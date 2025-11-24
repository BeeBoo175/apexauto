import { randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";
import { request as httpRequest } from "node:http";

const origin = process.env.APP_BASE_URL!;
const evidence = process.env.AUTH_EVIDENCE_DIR!;
const password = "Password123!";

function counts(): Record<string, number> {
  return JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
}

function fault(value: string) {
  writeFileSync(path.join(evidence, "fault.txt"), value);
}

test.afterEach(() => fault(""));

async function ingressLogin(localAddress: string, forgedAddress: string) {
  const body = JSON.stringify({
    loginKey: randomUUID(),
    email: "unregistered@example.invalid",
    password: "Password123!",
  });
  return new Promise<number>((resolve, reject) => {
    const request = httpRequest(
      new URL("/api/auth/login", origin),
      {
        method: "POST",
        localAddress,
        headers: {
          Origin: origin,
          "Content-Type": "application/json",
          "Content-Length": Buffer.byteLength(body),
          "X-ApexAuto-Client-IP": forgedAddress,
          "X-Forwarded-For": forgedAddress,
          "X-Forwarded-Proto": "https",
          Forwarded: `for=${forgedAddress}`,
        },
      },
      (response) => {
        response.resume();
        response.on("end", () => resolve(response.statusCode!));
      },
    );
    request.on("error", reject);
    request.end(body);
  });
}

test("sanitizing ingress and Next preserve independent login budgets despite forged headers", async () => {
  for (let attempt = 0; attempt < 5; attempt++) {
    expect(await ingressLogin("127.0.0.10", `198.51.100.${attempt + 10}`)).toBe(401);
  }

  expect(await ingressLogin("127.0.0.10", "198.51.100.99")).toBe(429);
  expect(await ingressLogin("127.0.0.11", "198.51.100.99")).toBe(401);
  for (let attempt = 1; attempt < 5; attempt++) {
    expect(await ingressLogin("127.0.0.11", "127.0.0.10")).toBe(401);
  }

  expect(await ingressLogin("127.0.0.11", "198.51.100.100")).toBe(429);
});

test("origin and body protection, malformed sessions and bounded body consumption", async ({
  request,
}) => {
  const before = counts()["/api/auth/login"] ?? 0;
  const login = { loginKey: randomUUID(), email: "unregistered@example.invalid", password };
  expect(
    (
      await request.post("/api/auth/login", {
        data: login,
        headers: { Origin: "https://outside.invalid" },
      })
    ).status(),
  ).toBe(403);
  expect(
    (
      await request.post("/api/auth/login", {
        data: "text",
        headers: { Origin: origin, "Content-Type": "text/plain" },
      })
    ).status(),
  ).toBe(415);
  expect(
    (
      await request.post("/api/auth/login", {
        data: "x".repeat(17 * 1024),
        headers: { Origin: origin, "Content-Type": "application/json" },
      })
    ).status(),
  ).toBe(413);
  expect(counts()["/api/auth/login"] ?? 0).toBe(before);
  fault("invalid-login");
  const invalid = await request.post("/api/auth/login", {
    data: login,
    headers: { Origin: origin },
  });
  expect(invalid.status()).toBe(502);
  expect(invalid.headers()["set-cookie"]).toBeUndefined();
  expect((await invalid.json()).uncertain).toBe(true);
  fault("slow-login");
  const started = Date.now();
  const slow = await request.post("/api/auth/login", { data: login, headers: { Origin: origin } });
  expect(slow.status()).toBe(502);
  expect(Date.now() - started).toBeLessThan(13_000);
  expect(slow.headers()["set-cookie"]).toBeUndefined();
});

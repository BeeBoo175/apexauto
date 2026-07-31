import { randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";
import { request as httpRequest } from "node:http";

const origin = process.env.APP_BASE_URL!;
const evidence = process.env.BROWSER_EVIDENCE_DIR!;
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

  const authRequests: [string, object][] = [
    ["login", login],
    [
      "register",
      {
        ...login,
        registrationKey: randomUUID(),
        legalName: "Test",
        username: "test.user",
        nric: "900415141234",
        mobile: "0123456789",
        residentialAddress: "Test address",
      },
    ],
    ["logout", {}],
    ["email-verification", { token: "controlled-token" }],
    ["email-verification/resend", { resendKey: randomUUID() }],
    ["password-recovery", { email: login.email }],
    ["password-reset", { token: "controlled-token", password }],
  ];
  for (const retry of ["37", "", "0", "1.5", "86401", "999999", "Wed, 21 Oct 2030 07:28:00 GMT"]) {
    fault(`retry:${retry}`);
    for (const [route, data] of authRequests) {
      const response = await request.post(`/api/auth/${route}`, {
        data,
        headers: { Origin: origin, Cookie: "apexauto_token=controlled-session" },
      });
      expect(response.status(), route).toBe(429);
      expect(response.headers()["cache-control"]).toContain("no-store");
      expect(response.headers()["retry-after"]).toBe(retry === "37" ? "37" : undefined);
      expect(await response.json()).toEqual({
        message:
          retry === "37"
            ? "Too many attempts. Wait 37 seconds before trying again."
            : "Too many attempts. Wait before trying again.",
      });
    }
  }

  fault("");
  const signedIn = await request.post("/api/auth/login", {
    data: { loginKey: randomUUID(), email: "nur.aisyah@example.invalid", password },
    headers: { Origin: origin },
  });
  expect(signedIn.status()).toBe(200);
  const cookie = signedIn.headers()["set-cookie"].split(";")[0];
  const headers = { Cookie: cookie };
  for (const retry of ["7", "", "86401", "1.5"]) {
    fault(`retry:${retry}`);
    const picture = await request.get(`/api/accounts/${randomUUID()}/picture`, {
      headers,
    });
    expect(picture.status()).toBe(429);
    expect(picture.headers()["retry-after"]).toBe(retry === "7" ? "7" : undefined);
    expect(picture.headers()["cache-control"]).toContain("no-store");
    expect(await picture.json()).toMatchObject({ status: 429, data: null, uncertain: false });
  }
  for (const [sessionFault, status] of [
    ["session-retry", 429],
    ["session-error", 502],
  ] as const) {
    fault(sessionFault);
    const response = await request.get("/api/staff/performance/headline", { headers });
    expect(response.status()).toBe(status);
    expect(response.headers()["retry-after"]).toBe(status === 429 ? "7" : undefined);
    expect(response.headers()["cache-control"]).toContain("no-store");
    expect(response.headers()["set-cookie"]).toBeUndefined();
    expect(await response.json()).toEqual({
      status,
      data: null,
      uncertain: status === 502,
      message:
        status === 429
          ? "Too many requests. Wait before trying again."
          : "Your session could not be confirmed.",
      ...(status === 429 ? { retryAfterSeconds: 7 } : {}),
    });
  }
  const conflicts = [
    "check-in-content-conflict",
    "check-in-unavailable",
    "vehicle-identity-conflict",
  ];
  const messages = new Set<string>();
  let actor: unknown;
  for (const code of [...conflicts, "unknown", "__proto__"]) {
    fault(`conflict:${code}`);
    const response = await request.get("/api/intake/appointments", { headers });
    expect(response.status()).toBe(409);
    const body = await response.json();
    expect(body.data).toBeNull();
    expect(body.uncertain).toBe(false);
    expect(JSON.stringify(body)).not.toContain("private provider detail");
    expect(body.actor.role).toBe("Customer");
    actor ??= body.actor;
    expect(body.actor).toEqual(actor);
    expect(body.code).toBe(conflicts.includes(code) ? code : undefined);
    if (conflicts.includes(code)) {
      messages.add(body.message);
    }
  }
  expect(messages.size).toBe(3);
  for (const retry of ["37", "", "86401", "1.5"]) {
    fault(`retry:${retry}`);
    const response = await request.get("/api/intake/appointments", { headers });
    expect(response.status()).toBe(429);
    expect(response.headers()["retry-after"]).toBe(retry === "37" ? "37" : undefined);
    expect(response.headers()["cache-control"]).toContain("no-store");
    expect(await response.json()).toEqual({
      status: 429,
      data: null,
      uncertain: false,
      message: "Too many requests. Wait before trying again.",
      actor,
      ...(retry === "37" ? { retryAfterSeconds: 37 } : {}),
    });
  }
  fault("invalid-success");
  const malformed = await request.get("/api/intake/appointments", { headers });
  expect(malformed.status()).toBe(502);
  expect(await malformed.json()).toMatchObject({ data: null, uncertain: true, actor });
});

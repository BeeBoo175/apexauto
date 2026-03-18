import { test, expect } from "@playwright/test";
import { parseAccount, parseSession, safeReturnTo } from "@/lib/auth/contracts";
import { trustedClientAddress } from "@/lib/server/http/client-address";
import { HttpError, parseRetryAfterSeconds, readBoundedJson } from "@/lib/http";
import { apiFetch } from "@/lib/api-browser";

test("return destinations stay inside the supported account route", () => {
  for (const destination of [
    "https://outside.invalid",
    "//outside.invalid",
    "/\\outside.invalid",
    "/account\n",
    "/%2f%2foutside.invalid",
    "/api/auth/logout",
    "/login",
    ["/sell"],
  ]) {
    expect(safeReturnTo(destination)).toBe("/account");
  }

  expect(safeReturnTo("/account?view=details#profile")).toBe("/account?view=details#profile");
  expect(safeReturnTo("/sell?draft=retained#details")).toBe("/sell?draft=retained#details");
});

test("client identity requires an explicitly selected single ingress address", () => {
  const headers = new Headers({
    "X-Forwarded-For": "198.51.100.99",
    Forwarded: "for=198.51.100.99",
    "X-ApexAuto-Client-IP": "198.51.100.10",
  });
  expect(trustedClientAddress(headers)).toBeNull();
  expect(trustedClientAddress(headers, "X-ApexAuto-Client-IP")).toBe("198.51.100.10");
  headers.set("X-ApexAuto-Client-IP", "2001:db8::10");
  expect(trustedClientAddress(headers, "X-ApexAuto-Client-IP")).toBe("2001:db8::10");

  for (const address of [
    "",
    "unknown",
    "198.51.100.10, 198.51.100.11",
    "198.51.100.10:80",
    "[::1]",
  ]) {
    headers.set("X-ApexAuto-Client-IP", address);
    expect(() => trustedClientAddress(headers, "X-ApexAuto-Client-IP")).toThrow();
  }

  expect(() => trustedClientAddress(new Headers(), "X-ApexAuto-Client-IP")).toThrow();
  expect(() => trustedClientAddress(headers, "invalid header")).toThrow();
});

test("only structurally valid future sessions can cross the cookie boundary", () => {
  const future = new Date(Date.now() + 60_000).toISOString();
  expect(parseSession({ accessToken: "abc.def.signature", expiresAt: future })?.accessToken).toBe(
    "abc.def.signature",
  );

  for (const session of [
    null,
    {},
    { accessToken: "abc.def.signature", expiresAt: "invalid" },
    { accessToken: "abc.def.signature", expiresAt: "2020-01-01T00:00:00Z" },
    { accessToken: "token;inject=value", expiresAt: future },
    { accessToken: `abc.def.${"x".repeat(4000)}`, expiresAt: future },
  ]) {
    expect(parseSession(session)).toBeNull();
  }
});

test("account snapshots validate identity and verification fields before rendering", () => {
  const account = {
    userId: "8bc9d7c9-1c94-420e-8193-a01b6c0b3e40",
    legalName: "Nur Aisyah binti Rahman",
    email: "aisyah@example.invalid",
    emailVerified: false,
  };
  expect(parseAccount(account)).toEqual(account);
  expect(parseAccount({ ...account, emailVerified: "false" })).toBeNull();
  expect(parseAccount({ ...account, userId: "00000000-0000-0000-0000-000000000000" })).toBeNull();
  expect(parseAccount({ ...account, legalName: " " })).toBeNull();
});

test("shared JSON reads retain UTF-8 characters split across chunks at the byte limit", async () => {
  const bytes = new TextEncoder().encode('{"customer":"Nur Aisyah 陈"}');
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const byte of bytes) {
        controller.enqueue(Uint8Array.of(byte));
      }
      controller.close();
    },
  });

  await expect(readBoundedJson(body, new AbortController().signal, bytes.length)).resolves.toEqual({
    customer: "Nur Aisyah 陈",
  });
  expect(body.locked).toBe(false);
});

test("oversized JSON reads cancel unread data and release the body", async () => {
  let cancelled = false;
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(new TextEncoder().encode('{"budget":2500}'));
    },
    cancel() {
      cancelled = true;
    },
  });

  await expect(readBoundedJson(body, new AbortController().signal, 4)).rejects.toMatchObject({
    status: 413,
  });
  expect(cancelled).toBe(true);
  expect(body.locked).toBe(false);
});

test("aborting a stalled JSON read rejects without waiting for another chunk", async () => {
  const abort = new AbortController();
  let cancelled = false;
  const body = new ReadableStream<Uint8Array>({
    cancel() {
      cancelled = true;
    },
  });
  const reading = readBoundedJson(body, abort.signal, 1024);
  abort.abort();

  await expect(reading).rejects.toMatchObject({ name: "AbortError" });
  expect(cancelled).toBe(true);
  expect(body.locked).toBe(false);
});

test("missing and malformed JSON bodies remain rejected after cleanup", async () => {
  expect(parseRetryAfterSeconds("1")).toBe(1);
  expect(parseRetryAfterSeconds("86400")).toBe(86400);
  for (const value of [
    null,
    "",
    "0",
    "-1",
    "1.5",
    "1e2",
    " 60",
    "60 ",
    "86401",
    "999999999999999999",
    "60, 120",
    "Wed, 21 Oct 2026 07:28:00 GMT",
  ]) {
    expect(parseRetryAfterSeconds(value)).toBeUndefined();
  }
  const signal = new AbortController().signal;
  await expect(readBoundedJson(null, signal, 1024)).rejects.toBeInstanceOf(HttpError);
  const body = new Response('{"budget":').body!;
  await expect(readBoundedJson(body, signal, 1024)).rejects.toBeInstanceOf(SyntaxError);
  expect(body.locked).toBe(false);
});

test("shared browser transport validates envelopes and preserves private actor requirements", async () => {
  const previousFetch = globalThis.fetch;
  const previousWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
  const events = new EventTarget();
  let sessionChanges = 0;
  events.addEventListener("apexauto-session-invalid", () => sessionChanges++);
  Object.defineProperty(globalThis, "window", { configurable: true, value: events });
  const parse = (value: unknown) => (value === "confirmed" ? value : null);
  const envelope = { status: 200, data: "confirmed", message: "", uncertain: false };

  try {
    globalThis.fetch = async () => Response.json(envelope);
    await expect(apiFetch("/api/intake/branches", parse)).resolves.toMatchObject({
      data: "confirmed",
    });
    await expect(
      apiFetch(
        "/api/staff/inspection/vehicles",
        parse,
        {},
        { deadlineMs: 1000, maximumResponseBytes: 1024, requireActor: true },
      ),
    ).rejects.toThrow("Unconfirmed service response");
    globalThis.fetch = async () => Response.json({ ...envelope, data: null });
    await expect(apiFetch("/api/intake/branches", parse)).rejects.toThrow(
      "Unconfirmed service response",
    );
    globalThis.fetch = async () => Response.json({ ...envelope, status: 201 });
    await expect(apiFetch("/api/intake/branches", parse)).rejects.toThrow(
      "Unreadable service response",
    );
    globalThis.fetch = async () =>
      Response.json(
        { status: 403, data: null, message: "Access unavailable", uncertain: false },
        { status: 403 },
      );
    await expect(
      apiFetch(
        "/api/staff/inspection/vehicles",
        parse,
        {},
        { deadlineMs: 1000, maximumResponseBytes: 1024, requireActor: true },
      ),
    ).resolves.toMatchObject({
      status: 403,
      data: null,
    });
    expect(sessionChanges).toBe(0);
    globalThis.fetch = async () =>
      Response.json(
        { status: 401, data: null, message: "Sign in again", uncertain: false },
        { status: 401 },
      );
    await expect(apiFetch("/api/staff/inspection/vehicles", parse)).resolves.toMatchObject({
      status: 401,
      data: null,
    });
    expect(sessionChanges).toBe(1);
    for (const [code, message] of [
      [
        "check-in-content-conflict",
        "This appointment was checked in with different details. Refresh to review the recorded check-in.",
      ],
      [
        "check-in-unavailable",
        "This appointment or its evidence is unavailable for check-in. Refresh the appointment before continuing.",
      ],
      [
        "vehicle-identity-conflict",
        "The VIN or registration already identifies a vehicle. Review the vehicle identity before continuing.",
      ],
    ]) {
      globalThis.fetch = async () =>
        Response.json(
          {
            status: 409,
            data: "confirmed",
            code,
            message: "Private provider text",
            uncertain: false,
          },
          { status: 409 },
        );
      await expect(apiFetch("/api/staff/intake/appointments/check-in", parse)).resolves.toEqual({
        status: 409,
        data: null,
        code,
        message,
        uncertain: false,
      });
    }
    for (const code of ["unknown-provider-code", "toString", "__proto__", null, 409, {}]) {
      globalThis.fetch = async () =>
        Response.json(
          { status: 409, data: null, code, message: "Generic conflict", uncertain: false },
          { status: 409 },
        );
      const result = await apiFetch("/api/staff/intake/appointments/check-in", parse);
      expect(result.code).toBeUndefined();
      expect(result.message).toBe("Generic conflict");
    }
    for (const retryAfter of ["60", "0", "-1", "86401", "not a duration", null]) {
      globalThis.fetch = async () =>
        Response.json(
          {
            status: 429,
            data: null,
            message: "Wait before retrying",
            uncertain: false,
            retryAfterSeconds: 999999999,
          },
          { status: 429, headers: retryAfter ? { "Retry-After": retryAfter } : {} },
        );
      const result = await apiFetch("/api/intake/branches", parse);
      expect(result.retryAfterSeconds).toBe(retryAfter === "60" ? 60 : undefined);
      expect(result.uncertain).toBe(false);
    }
    expect(sessionChanges).toBe(1);
  } finally {
    globalThis.fetch = previousFetch;
    if (previousWindow) {
      Object.defineProperty(globalThis, "window", previousWindow);
    } else {
      Reflect.deleteProperty(globalThis, "window");
    }
  }
});

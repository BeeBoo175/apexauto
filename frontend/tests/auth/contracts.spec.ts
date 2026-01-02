import { test, expect } from "@playwright/test";
import { parseAccount, parseSession, safeReturnTo } from "../../src/lib/auth/contracts";
import { trustedClientAddress } from "../../src/lib/server/http/client-address";

test("return destinations stay inside the supported account route", () => {
  for (const destination of [
    "https://outside.invalid",
    "//outside.invalid",
    "/\\outside.invalid",
    "/account\n",
    "/%2f%2foutside.invalid",
    "/api/auth/logout",
    "/login",
    "/sell?draft=retained#details",
    ["/sell"],
  ]) {
    expect(safeReturnTo(destination)).toBe("/account");
  }

  expect(safeReturnTo("/account?view=details#profile")).toBe("/account?view=details#profile");
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

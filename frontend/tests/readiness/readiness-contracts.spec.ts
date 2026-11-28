import { expect, test } from "@playwright/test";
import { createProbeBudget } from "@/lib/readiness/budget";
import { buildReport, classifyApiReply, overallState } from "@/lib/readiness/contracts";
import { allowedOrigin, configuredOrigins } from "@/lib/readiness/cors";

const now = new Date("2026-11-28T04:00:00Z");

test("a ready answer from the API makes the API and the database ready", () => {
  const report = buildReport({ status: 200, body: { status: "ready" } }, now);
  expect(report).toEqual({
    status: "ready",
    checkedAt: "2026-11-28T04:00:00.000Z",
    parts: { site: "ready", api: "ready", database: "ready", storage: "not-checked" },
  });
});

test("the API's own database failure names the database as down while the API answers", () => {
  const report = buildReport({ status: 503, body: { status: "unavailable" } }, now);
  expect(report.parts).toMatchObject({ api: "ready", database: "down" });
  expect(report.status).toBe("unavailable");
});

test("no answer, a host start-up page or a malformed body is waking, never ready", () => {
  for (const reply of [
    null,
    { status: 503, body: null },
    { status: 502, body: "<html>" },
    { status: 200, body: { status: "starting" } },
    { status: 200, body: null },
  ]) {
    expect(classifyApiReply(reply)).toEqual({ api: "waking", database: "unknown" });
    expect(buildReport(reply, now).status).toBe("waking");
  }
});

test("a part that is down outranks one that is waking, and unchecked storage never blocks ready", () => {
  const parts = { site: "ready", api: "waking", database: "down", storage: "not-checked" } as const;
  expect(overallState(parts)).toBe("unavailable");
  expect(overallState({ ...parts, api: "ready", database: "ready" })).toBe("ready");
  expect(overallState({ ...parts, api: "ready", database: "unknown" })).toBe("waking");
});

test("only configured origins are allowed, compared exactly", () => {
  const configured =
    "https://portfolio.example, https://preview.example , not-a-url, https://x.example/path";
  expect(configuredOrigins(configured)).toEqual([
    "https://portfolio.example",
    "https://preview.example",
  ]);
  expect(allowedOrigin("https://portfolio.example", configured)).toBe("https://portfolio.example");
  for (const origin of ["https://portfolio.example.evil", "http://portfolio.example", null, ""]) {
    expect(allowedOrigin(origin, configured)).toBeNull();
  }
  expect(allowedOrigin("https://portfolio.example", undefined)).toBeNull();
});

test("the probe budget admits its limit, then waits until the window ends", () => {
  const admit = createProbeBudget(2, 60_000);
  expect(admit(1_000)).toEqual({ admitted: true });
  expect(admit(2_000)).toEqual({ admitted: true });
  expect(admit(11_000)).toEqual({ admitted: false, retryAfterSeconds: 50 });
  expect(admit(61_000)).toEqual({ admitted: true });
});

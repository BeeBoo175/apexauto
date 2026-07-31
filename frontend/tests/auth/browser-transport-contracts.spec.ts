import { test, expect } from "@playwright/test";
import { apiFetch, forgetSnapshots } from "@/lib/api-browser";
import { readBranchChoices } from "@/lib/branch-choices";
import { parseIntakeConflict } from "@/lib/intake/errors";
import { apiReadError } from "@/lib/api";
import { readFailureMessage } from "@/lib/http";

test.beforeEach(forgetSnapshots);
test.afterEach(forgetSnapshots);

test("branch choices reuse confirmed pages until refresh while public picker reads stay fresh", async () => {
  const originalFetch = globalThis.fetch;
  const signal = new AbortController().signal;
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return Response.json({
      status: 200,
      data: { items: [], page: 1, pageSize: 25, total: calls },
      message: "",
      uncertain: false,
    });
  };

  try {
    const first = await readBranchChoices(1, signal, 25);
    expect(first.data).not.toBeNull();
    expect(await readBranchChoices(1, signal, 25)).toEqual(first);
    expect(calls).toBe(1);

    forgetSnapshots();
    expect((await readBranchChoices(1, signal, 25)).data?.total).toBe(2);
    expect(calls).toBe(2);

    for (const total of [3, 4]) {
      const current = await readBranchChoices(1, signal, 25, { fresh: true });
      expect(current.data?.total).toBe(total);
    }
    expect(calls).toBe(4);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("branch snapshots exclude failures, other queries, exports and write attempts", async () => {
  const originalFetch = globalThis.fetch;
  const parse = (value: unknown) => (value === "confirmed" ? value : null);
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return Response.json({ status: 200, data: "confirmed", message: "", uncertain: false });
  };

  try {
    const path = "/api/intake/branches?page=1";
    await apiFetch(path, parse);
    await apiFetch(path, parse);
    expect(calls).toBe(1);
    await apiFetch("/api/intake/branches?page=2", parse);
    expect(calls).toBe(2);

    for (const excluded of ["/api/staff/intake/export", "/api/intake/branches/other"]) {
      await apiFetch(excluded, parse);
      await apiFetch(excluded, parse);
    }
    expect(calls).toBe(6);

    globalThis.fetch = async () => {
      calls++;
      return limited("60");
    };
    await apiFetch("/api/staff/intake/assignments", parse, { method: "POST" });
    expect((await apiFetch(path, parse)).status).toBe(429);
    expect((await apiFetch(path, parse)).status).toBe(429);
    expect(calls).toBe(9);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function limited(seconds = "1") {
  return Response.json(
    { status: 429, data: null, message: "Busy", uncertain: false },
    { status: 429, headers: { "Retry-After": seconds } },
  );
}

test("browser GET retries once and retains the final rejection metadata", async () => {
  const originalFetch = globalThis.fetch;
  const parse = (value: unknown) => (value === "confirmed" ? value : null);
  try {
    for (const succeeds of [true, false]) {
      let calls = 0;
      const signals: (AbortSignal | null | undefined)[] = [];
      globalThis.fetch = async (_input, options) => {
        calls++;
        signals.push(options?.signal);
        if (calls === 1 || !succeeds) {
          return limited();
        }
        return Response.json({ status: 200, data: "confirmed", message: "", uncertain: false });
      };
      const result = await apiFetch("/api/intake/slots", parse);
      expect(calls).toBe(2);
      expect(signals[1]).toBe(signals[0]);
      expect(result.status).toBe(succeeds ? 200 : 429);
      if (succeeds) {
        expect(result.data).toBe("confirmed");
      } else {
        expect(result.retryAfterSeconds).toBe(1);
        expect(readFailureMessage(apiReadError("Unavailable", result), "Fallback")).toBe(
          "Too many requests. Try again in 1 seconds.",
        );
      }
    }
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("writes and long or missing Retry-After values never schedule another request", async () => {
  const originalFetch = globalThis.fetch;
  try {
    for (const [method, seconds] of [
      ["POST", "1"],
      ["PUT", "1"],
      ["GET", "60"],
      ["GET", "invalid"],
    ]) {
      let calls = 0;
      globalThis.fetch = async () => {
        calls++;
        return limited(seconds);
      };
      const result = await apiFetch("/api/intake/slots", () => null, { method });
      expect(result.status).toBe(429);
      expect(result.retryAfterSeconds).toBe(seconds === "invalid" ? undefined : Number(seconds));
      expect(calls).toBe(1);
    }
    expect(readFailureMessage(new Error("Private details"), "Fallback")).toBe("Fallback");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("caller cancellation and the original deadline abort the backoff without another fetch", async () => {
  const originalFetch = globalThis.fetch;
  try {
    for (const callerAborts of [true, false]) {
      let calls = 0;
      const controller = new AbortController();
      globalThis.fetch = async () => {
        calls++;
        return limited();
      };
      const request = apiFetch(
        "/api/intake/slots",
        () => null,
        { signal: controller.signal },
        {
          deadlineMs: callerAborts ? 1000 : 50,
        },
      );
      const rejected = expect(request).rejects.toMatchObject({
        name: callerAborts ? "AbortError" : "TimeoutError",
      });
      if (callerAborts) {
        await new Promise((resolve) => setTimeout(resolve, 50));
        controller.abort();
      }
      await rejected;
      expect(calls).toBe(1);
    }
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("a failed second transport retains the rejection while uncertain results are never retried", async () => {
  const originalFetch = globalThis.fetch;
  try {
    let calls = 0;
    globalThis.fetch = async () => {
      calls++;
      if (calls === 1) {
        return limited();
      }
      throw new TypeError("Connection unavailable");
    };
    await expect(apiFetch("/api/intake/slots", () => null)).resolves.toMatchObject({
      status: 429,
      retryAfterSeconds: 1,
      uncertain: false,
    });
    expect(calls).toBe(2);
    calls = 0;
    globalThis.fetch = async () => {
      calls++;
      return Response.json(
        { status: 429, data: null, message: "Unconfirmed", uncertain: true },
        { status: 429, headers: { "Retry-After": "1" } },
      );
    };
    await expect(apiFetch("/api/intake/slots", () => null)).resolves.toMatchObject({
      status: 429,
      uncertain: true,
    });
    expect(calls).toBe(1);
  } finally {
    globalThis.fetch = originalFetch;
  }
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
    await expect(apiFetch("/api/intake/slots", parse)).resolves.toMatchObject({
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
    await expect(apiFetch("/api/intake/slots", parse)).rejects.toThrow(
      "Unconfirmed service response",
    );
    globalThis.fetch = async () => Response.json({ ...envelope, status: 201 });
    await expect(apiFetch("/api/intake/slots", parse)).rejects.toThrow(
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
      await expect(
        apiFetch(
          "/api/staff/intake/appointments/check-ins",
          parse,
          {},
          { parseConflict: parseIntakeConflict },
        ),
      ).resolves.toEqual({
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
      const result = await apiFetch(
        "/api/staff/intake/appointments/check-ins",
        parse,
        {},
        { parseConflict: parseIntakeConflict },
      );
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
      const result = await apiFetch("/api/intake/slots", parse);
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

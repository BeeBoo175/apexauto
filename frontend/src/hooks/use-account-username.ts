"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { isRecord } from "@/lib/auth/contracts";
import { privateApiFetch } from "@/lib/api-browser";

const USERNAME_PATTERN = /^[A-Za-z0-9._]{3,30}$/;

function parseChangedUsername(value: unknown) {
  return isRecord(value) && typeof value.username === "string"
    ? { username: value.username }
    : null;
}

// One operation identity covers retries of the same request, so an uncertain change cannot apply twice.
export function useAccountUsername(initialUsername: string) {
  const [username, setUsername] = useState(initialUsername);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState("");
  const controller = useRef<AbortController | null>(null);
  const attempt = useRef<{ operationId: string; username: string } | null>(null);
  const { success, warning } = useToast();

  useEffect(() => () => controller.current?.abort(), []);

  // Returns true when the change is confirmed, so the caller can close its dialog.
  async function change(requested: string) {
    const next = requested.trim();
    if (controller.current) {
      return false;
    }
    if (next === username) {
      return true;
    }
    if (!USERNAME_PATTERN.test(next)) {
      setProblem("Use 3 to 30 letters, numbers, dots or underscores.");
      return false;
    }
    if (attempt.current?.username !== next) {
      attempt.current = { operationId: crypto.randomUUID(), username: next };
    }
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    setProblem("");
    try {
      const result = await privateApiFetch(
        "/api/account/username",
        parseChangedUsername,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(attempt.current),
          signal: operation.signal,
        },
        { deadlineMs: 17_000 },
      );
      if (result.uncertain || result.status >= 500) {
        throw new Error("Unconfirmed username change");
      }
      if (result.status === 200 && result.data) {
        attempt.current = null;
        setUsername(result.data.username);
        success("Username updated.");
        return true;
      }
      setProblem(result.message);
      return false;
    } catch {
      if (!operation.signal.aborted) {
        warning(
          "The result is unconfirmed. Retry the same username when the service is available.",
        );
      }
      return false;
    } finally {
      controller.current = null;
      if (!operation.signal.aborted) {
        setBusy(false);
      }
    }
  }

  return { username, busy, problem, clearProblem: () => setProblem(""), change };
}

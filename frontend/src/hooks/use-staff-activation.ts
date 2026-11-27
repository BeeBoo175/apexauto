"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, isRecord } from "@/lib/auth/contracts";
import { parseStaffInvitation, type StaffInvitationPreview } from "@/lib/auth/staff-invitation";
import { writeAddress } from "@/lib/browser-url";

type Preview =
  | { state: "loading" }
  | { state: "unavailable"; message: string }
  | { state: "ready"; invitation: StaffInvitationPreview };

// The emailed link keeps its token in the fragment. Opening the page only reads the invitation; the employee
// deliberately submits a password and username, and an uncertain submit is retried with the same values.
export function useStaffActivation() {
  const [token, setToken] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview>({ state: "loading" });
  const [password, setPassword] = useState("");
  const [username, setUsername] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState("");
  const controller = useRef<AbortController | null>(null);
  const { success, warning } = useToast();
  const router = useRouter();

  useEffect(() => {
    const fragmentToken = new URLSearchParams(window.location.hash.slice(1)).get("token");
    const reading = new AbortController();
    queueMicrotask(() => {
      setToken(fragmentToken);
      if (!fragmentToken) {
        setPreview({ state: "unavailable", message: "This invitation link is missing its token." });
      }
    });
    if (fragmentToken) {
      void readPreview(fragmentToken, reading.signal).then((result) => {
        if (!reading.signal.aborted) {
          setPreview(result);
        }
      });
    }
    return () => {
      reading.abort();
      controller.current?.abort();
    };
  }, []);

  async function submit() {
    if (!token || controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    setProblem("");
    try {
      const response = await fetch("/api/auth/staff-invitation/activation", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, password, username: username.trim() }),
        signal: AbortSignal.any([attempt.signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
      });
      const receipt: unknown = await response.json();
      if (response.ok && isRecord(receipt) && receipt.ok === true) {
        writeAddress("/activate-account");
        success("Account activated. Sign in with your email and password.");
        router.push("/login");
        return;
      }
      if (
        !response.ok &&
        isRecord(receipt) &&
        typeof receipt.message === "string" &&
        receipt.uncertain !== true &&
        response.status < 500
      ) {
        setProblem(receipt.message);
        return;
      }
      throw new Error("Unconfirmed activation");
    } catch {
      if (!attempt.signal.aborted) {
        warning("The result is unconfirmed. Keep this page open and retry with the same details.");
      }
    } finally {
      controller.current = null;
      setBusy(false);
    }
  }

  return { token, preview, password, setPassword, username, setUsername, busy, problem, submit };
}

async function readPreview(token: string, signal: AbortSignal): Promise<Preview> {
  try {
    const response = await fetch("/api/auth/staff-invitation/preview", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ token }),
      signal: AbortSignal.any([signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
    });
    const body: unknown = await response.json();
    const invitation = isRecord(body) ? parseStaffInvitation(body.invitation) : null;
    if (response.ok && invitation) {
      return { state: "ready", invitation };
    }
    const message =
      isRecord(body) && typeof body.message === "string"
        ? body.message
        : "This invitation could not be opened. Reload the page to try again.";
    return { state: "unavailable", message };
  } catch {
    return {
      state: "unavailable",
      message: "This invitation could not be opened. Reload the page to try again.",
    };
  }
}

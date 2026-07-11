"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { isUnconfirmedResult } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import type { AccountEditDraft } from "@/lib/accounts/account-edit";
import type { StaffAccountChangeDraft } from "@/lib/accounts/account-administration";
import type { AccountSuspensionDraft } from "@/lib/accounts/account-suspension";
import {
  customerSupportActions,
  parseNricReveal,
  type CustomerSupportAction,
  type CustomerSupportDraft,
} from "@/lib/accounts/customer-support";
import { parseAcknowledgement } from "@/lib/accounts/staff-invitations";

// Administrator account commands. An unconfirmed command keeps its exact body and operation id, so the
// next attempt of the same command replays it instead of becoming a second operation.
export function useAccountCommand() {
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const attempts = useRef(new Map<string, string>());
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();

  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => {
    if (!busy && !uncertain) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [busy, uncertain]);

  async function send(path: string, method: "POST" | "PUT", body: unknown, done: string) {
    if (controller.current) {
      return false;
    }
    const key = attemptKey(path, method, body);
    const replaying = attempts.current.has(key);
    const payload = attempts.current.get(key) ?? (body === null ? "" : JSON.stringify(body));
    attempts.current.set(key, payload);
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    try {
      const result = await privateApiFetch(path, parseAcknowledgement, {
        method,
        headers: { "Content-Type": "application/json" },
        body: payload === "" ? undefined : payload,
        signal: operation.signal,
      });
      if (operation.signal.aborted) {
        return false;
      }
      if (isUnconfirmedResult(result, replaying)) {
        throw new Error("Unconfirmed mutation");
      }
      attempts.current.delete(key);
      setUncertain(attempts.current.size > 0);
      if (result.status !== 200 || !result.data) {
        toast.error(
          result.message || "The request was not confirmed. Check the list before retrying.",
        );
        return false;
      }
      toast.success(done);
      return true;
    } catch {
      if (!operation.signal.aborted) {
        setUncertain(true);
        toast.warning("The result is unconfirmed. Retry the same request to reconcile it.");
      }
      return false;
    } finally {
      if (!operation.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }

  // The NRIC comes back in the response, so it is read here rather than acknowledged.
  async function reveal(id: string, draft: CustomerSupportDraft) {
    if (busy) {
      return null;
    }
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `/api/customer-accounts/${id}/support/nric-reveal`,
        parseNricReveal,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(draft),
        },
      );
      if (result.status !== 200 || !result.data) {
        toast.error(result.message || "The number could not be revealed. Nothing was shown.");
        return null;
      }
      return result.data.nric;
    } catch {
      toast.error("The number could not be revealed. Nothing was shown.");
      return null;
    } finally {
      setBusy(false);
    }
  }

  return {
    busy,
    uncertain,
    reveal,
    support: (id: string, action: CustomerSupportAction, draft: CustomerSupportDraft) =>
      send(
        `/api/customer-accounts/${id}/support/${action}`,
        "POST",
        draft,
        customerSupportActions[action].done,
      ),
    invite: (body: unknown) => send("/api/staff-invitations", "POST", body, "Invitation sent."),
    resend: (id: string) =>
      send(`/api/staff-invitations/${id}/resends`, "POST", null, "Invitation sent again."),
    changeEmail: (id: string, email: string) =>
      send(
        `/api/staff-invitations/${id}/email`,
        "PUT",
        { email },
        "Email changed and invitation sent.",
      ),
    edit: (id: string, draft: AccountEditDraft) =>
      send(`/api/accounts/${id}/edits`, "POST", draft, "Details saved."),
    suspend: (id: string, draft: AccountSuspensionDraft) =>
      send(`/api/accounts/${id}/suspensions`, "POST", draft, "Account suspended."),
    lift: (id: string, draft: AccountSuspensionDraft) =>
      send(`/api/accounts/${id}/suspensions/lift`, "POST", draft, "Suspension lifted."),
    deactivate: (id: string, draft: StaffAccountChangeDraft) =>
      send(`/api/staff-accounts/${id}/deactivations`, "POST", draft, "Account deactivated."),
    // The operation id is part of the captured attempt, so a retry after a lost response replays it.
    reactivate: (id: string) =>
      send(
        `/api/staff-accounts/${id}/reactivations`,
        "POST",
        { operationId: crypto.randomUUID() },
        "Account reactivated.",
      ),
    transfer: (id: string, draft: StaffAccountChangeDraft) =>
      send(`/api/staff-accounts/${id}/transfers`, "POST", draft, "Employee transferred."),
    revoke: (id: string) =>
      send(`/api/staff-invitations/${id}/revocations`, "POST", null, "Invitation revoked."),
  };
}

// A command with an operation id is one attempt per target; one without it is one attempt per body.
function attemptKey(path: string, method: string, body: unknown) {
  const hasOperation = typeof body === "object" && body !== null && "operationId" in body;
  return hasOperation ? `${method} ${path}` : `${method} ${path} ${JSON.stringify(body)}`;
}

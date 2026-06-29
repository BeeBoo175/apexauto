"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
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

// One administrator account command at a time. A command that was not confirmed is reported and never retried silently.
export function useAccountCommand() {
  const [busy, setBusy] = useState(false);
  const toast = useToast();

  async function send(path: string, method: "POST" | "PUT", body: unknown, done: string) {
    if (busy) {
      return false;
    }
    setBusy(true);
    try {
      const result = await privateApiFetch(path, parseAcknowledgement, {
        method,
        headers: { "Content-Type": "application/json" },
        body: body === null ? undefined : JSON.stringify(body),
      });
      if (result.status !== 200 || !result.data) {
        toast.error(
          result.message || "The request was not confirmed. Check the list before retrying.",
        );
        return false;
      }
      toast.success(done);
      return true;
    } catch {
      toast.error("The request could not be confirmed. Refresh the list before retrying.");
      return false;
    } finally {
      setBusy(false);
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

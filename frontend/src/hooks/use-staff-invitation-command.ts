"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { parseAcknowledgement } from "@/lib/accounts/staff-invitations";

// One invitation command at a time. A command that was not confirmed is reported and never retried silently.
export function useStaffInvitationCommand() {
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

  return {
    busy,
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
    revoke: (id: string) =>
      send(`/api/staff-invitations/${id}/revocations`, "POST", null, "Invitation revoked."),
  };
}

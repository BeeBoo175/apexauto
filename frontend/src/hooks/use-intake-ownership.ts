"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import { parseIntakeConflict } from "@/lib/intake/errors";
import type { AppointmentDetail, AppointmentHandler } from "@/lib/staff/contracts";
import { parseAppointmentHandlers } from "@/lib/staff/parse";
import {
  ownershipMatches,
  parseOwnershipResult,
  type OwnershipAction,
} from "@/lib/staff/ownership";
import { useApiCommand } from "./use-api-command";
import { useEditGuard } from "./use-edit-guard";

export function useIntakeOwnership({
  actor,
  detail,
  onChanged,
}: {
  actor: CurrentActor;
  detail: AppointmentDetail;
  onChanged: () => Promise<void>;
}) {
  const [handlerId, setHandlerId] = useState("");
  const [reason, setReason] = useState("");
  const [choices, setChoices] = useState<AppointmentHandler[] | null>(null);
  const [reading, setReading] = useState(false);
  const [action, setAction] = useState<OwnershipAction>("claims");
  const capturedAction = useRef<OwnershipAction>("claims");
  const controller = useRef<AbortController | null>(null);
  const submitting = useRef(false);
  const toast = useToast();
  const path = `/api/staff/intake/appointments/${detail.id}`;
  const command = useApiCommand(
    path,
    parseOwnershipResult,
    (result, request) =>
      ownershipMatches(result, request, detail.id, actor.userId, capturedAction.current),
    (url, parse, options) =>
      privateApiFetch(`${url}/${capturedAction.current}`, parse, options, {
        parseConflict: parseIntakeConflict,
      }),
  );
  const locked = command.busy || command.uncertain;
  const dirty = Boolean(handlerId || reason);
  useEditGuard({
    dirty,
    pending: command.busy,
    uncertain: command.uncertain,
    label: "Intake ownership",
    discard,
  });
  useLoadingProgress("Updating intake ownership", command.busy);
  useEffect(() => () => controller.current?.abort(), []);

  function discard() {
    if (locked) {
      return;
    }
    setHandlerId("");
    setReason("");
  }

  async function loadHandlers() {
    if (controller.current || locked) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setReading(true);
    try {
      const result = await privateApiFetch(`${path}/handlers`, parseAppointmentHandlers, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data) {
        throw new Error("Handler choices unavailable");
      }
      setChoices(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Handler choices could not be refreshed. Existing choices remain shown.");
      }
    } finally {
      if (!attempt.signal.aborted) {
        setReading(false);
      }
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }

  async function submit(nextAction: OwnershipAction) {
    if (submitting.current) {
      return;
    }
    submitting.current = true;
    if (!command.uncertain) {
      capturedAction.current = nextAction;
      setAction(nextAction);
    }
    try {
      const result = await command.submit({
        operationId: crypto.randomUUID(),
        expectedVersion: detail.ownership?.version ?? 0,
        handlerId: nextAction === "assignments" ? handlerId : null,
        reason: reason.trim() || null,
      });
      if (result?.status === 200 && result.data) {
        setHandlerId("");
        setReason("");
        await onChanged();
        return;
      }
      if (result?.status === 409 || result?.status === 404) {
        await onChanged();
      }
    } finally {
      submitting.current = false;
    }
  }

  return {
    handlerId,
    setHandlerId,
    reason,
    setReason,
    choices,
    reading,
    loadHandlers,
    dirty,
    discard,
    action,
    locked,
    command,
    submit,
  };
}

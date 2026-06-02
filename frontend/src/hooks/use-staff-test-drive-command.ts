"use client";

import { useEffect, useRef, useState } from "react";
import { useApiCommand } from "./use-api-command";
import { useEditGuard } from "./use-edit-guard";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { parseTestDriveHandlers } from "@/lib/test-drives/staff";
import {
  parseTestDriveStaffResponse,
  shapeTestDriveStaffCommand,
  type TestDriveStaffCommand,
} from "@/lib/test-drives/staff-commands";
import type { CurrentActor } from "@/lib/auth/actor";
import type { StaffTestDriveDetail } from "@/lib/test-drives/staff-query";

export const testDriveCommandLabels: Record<TestDriveStaffCommand, string> = {
  claims: "Claim test drive",
  assignments: "Assign handler",
  releases: "Release test drive",
  cancellations: "Cancel test drive",
  starts: "Start test drive",
  outcomes: "Record outcome",
};

interface CapturedCommand {
  kind: TestDriveStaffCommand;
  body: Record<string, unknown>;
}

export function useStaffTestDriveCommand(
  actor: CurrentActor,
  detail: StaffTestDriveDetail,
  refresh: (kind?: TestDriveStaffCommand) => Promise<boolean>,
) {
  const [action, setAction] = useState<TestDriveStaffCommand | "">("");
  const [reason, setReason] = useState("");
  const [notes, setNotes] = useState("");
  const [handlerId, setHandlerId] = useState("");
  const [present, setPresent] = useState(false);
  const [licence, setLicence] = useState(false);
  const [outcome, setOutcome] = useState<"Completed" | "NotCompleted">("Completed");
  const [captured, setCaptured] = useState<CapturedCommand | null>(null);
  const [confirmed, setConfirmed] = useState<CapturedCommand | null>(null);
  const [needsRefresh, setNeedsRefresh] = useState(false);
  const [handlers, setHandlers] = useState<NonNullable<
    ReturnType<typeof parseTestDriveHandlers>
  > | null>(null);
  const [readingHandlers, setReadingHandlers] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  const { booking } = detail;
  const command = useApiCommand(
    `/api/staff/test-drives/${booking.id}/${captured?.kind ?? "claims"}`,
    (value) =>
      captured
        ? parseTestDriveStaffResponse(captured.kind, value, captured.body, booking.id, actor)
        : null,
    undefined,
    privateApiFetch,
  );
  const locked = command.busy || command.uncertain;
  const dirty = Boolean(action || reason || notes || handlerId || present || licence);
  const terminal = ["Completed", "NotCompleted", "Cancelled", "NoShow"].includes(booking.status);
  const manager = actor.role === "BranchManager";
  const handler = booking.handlerId === actor.userId && booking.handlerEligible;
  const actions: TestDriveStaffCommand[] = [];
  if (!terminal && actor.role !== "GeneralManager") {
    if (!booking.handlerId && !detail.start) {
      actions.push("claims");
    }
    if (manager) {
      actions.push("assignments");
    }
    if (booking.handlerId && !detail.start && (handler || manager)) {
      actions.push("releases");
    }
    if (!detail.start && (handler || manager)) {
      actions.push("cancellations");
    }
    if (handler && booking.assignmentId) {
      if (!detail.start) {
        actions.push("starts");
      }
      actions.push("outcomes");
    }
  }

  function clearDraft() {
    setAction("");
    setReason("");
    setNotes("");
    setHandlerId("");
    setPresent(false);
    setLicence(false);
    setCaptured(null);
  }

  function discard() {
    if (!locked) {
      clearDraft();
    }
  }

  useEditGuard({
    dirty,
    pending: command.busy,
    uncertain: command.uncertain,
    label: "Test drive",
    discard,
  });
  useEffect(() => () => controller.current?.abort(), []);

  async function loadHandlers() {
    if (!manager || controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setReadingHandlers(true);
    try {
      const result = await privateApiFetch(
        `/api/staff/test-drives/${booking.id}/handlers`,
        parseTestDriveHandlers,
        { signal: attempt.signal },
      );
      if (!attempt.signal.aborted && result.data) {
        setHandlers(result.data);
      } else if (!attempt.signal.aborted) {
        toast.error("Handler choices could not be loaded. Retry to choose a current handler.");
      }
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Handler choices could not be loaded. Retry to choose a current handler.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setReadingHandlers(false);
      }
    }
  }

  function chooseAction(value: TestDriveStaffCommand | "") {
    setAction(value);
    if (value === "assignments") {
      void loadHandlers();
    }
    if (value === "outcomes" && !detail.start) {
      setOutcome("NotCompleted");
    }
  }

  function prepare() {
    if (!action || !actions.includes(action) || locked || needsRefresh) {
      return;
    }
    const requiresReason =
      action === "cancellations" ||
      (action === "assignments" && Boolean(booking.handlerId)) ||
      (action === "outcomes" && outcome === "NotCompleted");
    if (requiresReason && !reason.trim()) {
      toast.error("Enter a reason for this action.");
      return;
    }
    const body = shapeTestDriveStaffCommand(action, {
      operationId: crypto.randomUUID(),
      expectedVersion: booking.ownershipVersion,
      handlerId: action === "assignments" ? handlerId : null,
      assignmentId: booking.assignmentId,
      customerPresent: present,
      drivingLicenceChecked: licence,
      kind: outcome,
      reason: requiresReason ? reason.trim() : null,
      notes: notes.trim() || null,
    });
    if (!body) {
      toast.error(
        "Complete the required action fields. Notes and reasons allow up to 1,000 characters without control characters.",
      );
      return;
    }
    setCaptured({ kind: action, body });
  }

  async function reconcileRead(kind?: TestDriveStaffCommand) {
    const refreshed = await refresh(kind);
    if (refreshed) {
      setNeedsRefresh(false);
    }
    return refreshed;
  }

  async function submit() {
    if (!captured) {
      return;
    }
    const result = await command.submit(captured.body);
    if (!result) {
      return;
    }
    if (result.status === 200 && result.data) {
      setConfirmed(captured);
      setNeedsRefresh(true);
      clearDraft();
      await reconcileRead(captured.kind);
      return;
    }
    setCaptured(null);
    setNeedsRefresh(true);
    await reconcileRead();
  }

  return {
    action,
    chooseAction,
    actions,
    reason,
    setReason,
    notes,
    setNotes,
    handlerId,
    setHandlerId,
    present,
    setPresent,
    licence,
    setLicence,
    outcome,
    setOutcome,
    handlers,
    readingHandlers,
    loadHandlers,
    captured,
    closeConfirmation: () => setCaptured(null),
    confirmed,
    needsRefresh,
    reconcileRead,
    dirty,
    discard,
    locked,
    command,
    prepare,
    submit,
  };
}

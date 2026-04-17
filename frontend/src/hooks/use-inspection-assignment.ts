"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useApiCommand } from "./use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseAssignment } from "@/lib/inspection/parse-report";
import { parseTechnicians } from "@/lib/inspection/parse";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Page } from "@/lib/api";
import type { Technician } from "@/lib/inspection/contracts";

export function useInspectionAssignment({
  actor,
  vehicleId,
  version,
  initialChoices,
  onLocked,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  vehicleId: string;
  version: number;
  initialChoices: Page<Technician> | null;
  onLocked: (locked: boolean) => void;
  onConfirmed: (id: string) => void;
  onDraft?: (dirty: boolean) => void;
}) {
  const [choices, setChoices] = useState(initialChoices);
  const [technicianId, setTechnicianId] = useState(
    actor.role === "WorkshopTechnician" ? actor.userId : "",
  );
  const [reason, setReason] = useState("");
  const [reading, setReading] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  const command = useApiCommand(
    `vehicles/${vehicleId}/assignments`,
    parseAssignment,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.vehicleId === vehicleId &&
      r.actorId === actor.userId &&
      r.technicianId === body.technicianId &&
      r.expectedVersion === body.expectedVersion,
    inspectionFetch,
  );
  const locked = command.busy || command.uncertain;
  useEffect(() => () => controller.current?.abort(), []);

  async function load(page: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setReading(true);
    try {
      const result = await inspectionFetch(
        `technicians?page=${page}&pageSize=25`,
        parseTechnicians,
        { signal: attempt.signal },
      );
      if (!attempt.signal.aborted && result.data) {
        setChoices(result.data);
      }
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Technician choices are unavailable. Existing choices remain shown.");
      }
    } finally {
      if (!attempt.signal.aborted) {
        setReading(false);
      }
      controller.current = null;
    }
  }

  function changeTechnician(value: string) {
    setTechnicianId(value);
    onDraft?.(true);
  }

  function changeReason(value: string) {
    setReason(value);
    onDraft?.(true);
  }

  async function submit() {
    onLocked(true);
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      technicianId,
      expectedVersion: version,
      reason: reason.trim() || null,
    });
    if (result) {
      onLocked(false);
    }
    if (result?.data && result.status === 200) {
      onConfirmed(result.data.inspectionId);
    }
  }

  return {
    choices,
    technicianId,
    reason,
    reading,
    locked,
    command,
    load,
    changeTechnician,
    changeReason,
    submit,
  };
}

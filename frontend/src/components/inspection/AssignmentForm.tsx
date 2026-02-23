"use client";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Select, Textarea } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import { useApiCommand } from "@/hooks/use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseAssignment } from "@/lib/inspection/parse-report";
import { parseTechnicians } from "@/lib/inspection/parse";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Page } from "@/lib/api";
import type { Technician } from "@/lib/inspection/contracts";
import styles from "./Inspection.module.css";

export function AssignmentForm({
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
    `vehicles/${vehicleId}/assignment`,
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

  return (
    <form
      className={styles.form}
      onSubmit={async (event) => {
        event.preventDefault();
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
      }}
    >
      {actor.role === "BranchManager" && (
        <>
          <Select
            label="Workshop technician"
            value={technicianId}
            required
            disabled={locked || reading}
            onChange={(e) => {
              setTechnicianId(e.target.value);
              onDraft?.(true);
            }}
          >
            <option value="">Choose a technician</option>
            {technicianId && !choices?.items.some((t) => t.id === technicianId) && (
              <option value={technicianId}>Selected technician</option>
            )}
            {choices?.items.map((t) => (
              <option key={t.id} value={t.id}>
                {t.identity}
              </option>
            ))}
          </Select>
          {(!choices || choices.total > choices.pageSize) && (
            <div className={styles.actions}>
              <Button
                size="compact"
                variant="secondary"
                type="button"
                disabled={locked || reading || choices?.page === 1}
                onClick={() => void load(choices ? choices.page - 1 : 1)}
              >
                {choices ? "Previous technicians" : "Load technicians"}
              </Button>
              {choices && (
                <Button
                  size="compact"
                  variant="secondary"
                  type="button"
                  disabled={locked || reading || choices.page * choices.pageSize >= choices.total}
                  onClick={() => void load(choices.page + 1)}
                >
                  More technicians
                </Button>
              )}
            </div>
          )}
          <Textarea
            label="Assignment reason"
            required={version > 0}
            maxLength={1000}
            value={reason}
            disabled={locked}
            onChange={(e) => {
              setReason(e.target.value);
              onDraft?.(true);
            }}
          />
        </>
      )}
      {actor.role === "WorkshopTechnician" && (
        <p>Claim this vehicle for your inspection checklist.</p>
      )}
      {command.uncertain && (
        <p role="alert">
          Assignment is unconfirmed. Keep this window open and reconcile the captured request.
        </p>
      )}
      <Button size="compact" type="submit" disabled={command.busy || reading}>
        {command.uncertain ? "Reconcile assignment" : "Confirm assignment"}
      </Button>
    </form>
  );
}

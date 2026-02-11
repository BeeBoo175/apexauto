"use client";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useIntakeCommand } from "@/hooks/use-intake-command";
import { intakeFetch } from "@/lib/intake/browser";
import type { ApiResult } from "@/lib/intake/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import { emptyCheckIn, type AppointmentDetail, type CheckInDraft } from "@/lib/staff/contracts";
import { checkInMatches, parseCheckIn, parseDetail } from "@/lib/staff/parse";
import { formatMalaysianDate, formatMalaysianDateTime } from "@/lib/date-format";
import { CheckInFields } from "./CheckInFields";
import { AppointmentStatus } from "./AppointmentStatus";
import styles from "./AppointmentWorkbench.module.css";

export function AppointmentWorkbench({
  id,
  actor,
  initial,
  actionsContainer,
  draft,
  onDraft,
  onLock,
  onConfirmed,
}: {
  id: string;
  actor: CurrentActor;
  initial: ApiResult<AppointmentDetail>;
  actionsContainer?: HTMLElement | null;
  draft?: CheckInDraft;
  onDraft?: (draft: CheckInDraft) => void;
  onLock?: (locked: boolean) => void;
  onConfirmed?: () => void;
}) {
  useInitialToast(initial.message);
  const toast = useToast();
  const [snapshot, setSnapshot] = useState(initial);
  const [localDraft, setLocalDraft] = useState(emptyCheckIn);
  const [ready, setReady] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const command = useIntakeCommand(
    `/api/staff/intake/appointments/${id}/check-in`,
    parseCheckIn,
    (receipt, request) =>
      receipt.appointmentId === id &&
      receipt.staffId === actor.userId &&
      checkInMatches(receipt, request),
  );
  const value = draft ?? localDraft;
  const locked = command.busy || command.uncertain;
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => controller.current?.abort();
  }, []);
  const detail = snapshot.data;

  async function refresh() {
    if (controller.current || locked) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setRefreshing(true);
    try {
      const result = await intakeFetch(`/api/staff/intake/appointments/${id}`, parseDetail, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || result.data?.id !== id) {
        throw new Error("Detail unavailable");
      }
      setSnapshot(result);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "The appointment could not be refreshed. Existing details and your draft remain shown.",
        );
      }
    } finally {
      if (!attempt.signal.aborted) {
        setRefreshing(false);
      }
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }
  async function checkIn() {
    onLock?.(true);
    const result = await command.submit({
      ...value,
      mileageKilometres: Number(value.mileageKilometres),
    });
    if (result) {
      onLock?.(false);
    }
    if (result?.status !== 200 || !result.data) {
      return;
    }
    setSnapshot((current) => ({
      ...current,
      data: current.data
        ? {
            ...current.data,
            status: "CheckedIn",
          }
        : null,
    }));
    onConfirmed?.();
  }
  const refreshControl = (
    <RefreshButton
      label="Refresh appointment"
      busy={refreshing}
      disabled={!ready || locked}
      onClick={() => void refresh()}
    />
  );

  return (
    <div className={styles.workbench}>
      {actionsContainer && createPortal(refreshControl, actionsContainer)}
      {actionsContainer === undefined && (
        <div className={styles.toolbar}>
          <span className={styles.sectionTitle}>Reception workbench</span>
          {refreshControl}
        </div>
      )}
      {detail && (
        <>
          <div className={styles.recordHeading}>
            <strong>{detail.registrationPlate}</strong>
            <AppointmentStatus status={detail.status} />
          </div>
          <dl className={styles.details}>
            <div>
              <dt>Branch</dt>
              <dd>{detail.branchName}</dd>
            </div>
            <div>
              <dt>Customer</dt>
              <dd>{detail.customerName}</dd>
            </div>
            <div>
              <dt>Inspection</dt>
              <dd>{formatMalaysianDateTime(detail.startsAt)}</dd>
            </div>
            <div>
              <dt>Booked vehicle</dt>
              <dd>
                {detail.make} {detail.model} {detail.trim} · {detail.manufactureYear}
              </dd>
            </div>
            <div>
              <dt>Declared mileage</dt>
              <dd>{detail.mileageKilometres.toLocaleString("en-MY")} km</dd>
            </div>
            <div>
              <dt>Reference</dt>
              <dd className={styles.reference}>{detail.id}</dd>
            </div>
          </dl>
          {detail.noShowAt && (
            <p className={styles.help}>
              Recorded no-show effective {formatMalaysianDateTime(detail.noShowAt)}.
            </p>
          )}
          {detail.attendanceCorrectedAt && (
            <div className={styles.stack}>
              <p>Attendance corrected {formatMalaysianDateTime(detail.attendanceCorrectedAt)}.</p>
              <p>{detail.correctionReason}</p>
              <p className={styles.reference}>Manager reference: {detail.correctedBy}</p>
            </div>
          )}
          {command.receipt && (
            <div className={styles.stack}>
              <h3>Check-in confirmed</h3>
              <p>{formatMalaysianDateTime(command.receipt.checkedInAt)}</p>
              <p>
                Vehicle reference:{" "}
                <span className={styles.reference}>{command.receipt.vehicleId}</span>
              </p>
              <p className={styles.help}>
                The vehicle is pending inspection. The booked vehicle details and declared mileage
                are retained.
              </p>
            </div>
          )}
          {actor.role === "GeneralManager" && (
            <p className={styles.help}>Your role has read-only intake access.</p>
          )}
          {actor.role !== "GeneralManager" && detail.status === "Booked" && !command.receipt && (
            <form
              method="post"
              className={styles.protocol}
              onSubmit={(event) => {
                event.preventDefault();
                void checkIn();
              }}
            >
              <h3>Physical check-in</h3>
              <p className={styles.help}>
                Check-in is available only on the booked Malaysian date,{" "}
                {formatMalaysianDate(detail.startsAt)}. Confirm the physical vehicle and customer.
                The service checks current eligibility.
              </p>
              <CheckInFields
                draft={value}
                onChange={onDraft ?? setLocalDraft}
                disabled={!ready || locked || refreshing}
              />
              <div className={styles.actionFooter}>
                <p className={styles.help}>
                  Record the physical arrival. Booked facts remain unchanged.
                </p>
                <Button
                  size="compact"
                  type="submit"
                  disabled={!ready || command.busy || refreshing}
                >
                  {commandLabel(command.uncertain)}
                </Button>
              </div>
            </form>
          )}
        </>
      )}
    </div>
  );
}

function commandLabel(uncertain: boolean) {
  if (uncertain) {
    return "Retry same check-in";
  }
  return "Confirm check-in";
}

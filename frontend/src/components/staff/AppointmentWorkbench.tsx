"use client";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { useEditGuard } from "@/hooks/use-edit-guard";
import { Button } from "@/components/ui/Button";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useApiCommand } from "@/hooks/use-api-command";
import { apiFetch } from "@/lib/api-browser";
import { parseIntakeConflict } from "@/lib/intake/errors";
import type { ApiResult } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { emptyCheckIn, type AppointmentDetail, type CheckInDraft } from "@/lib/staff/contracts";
import { checkInMatches, correctionMatches, parseCheckIn, parseDetail } from "@/lib/staff/parse";
import { calendarDate } from "@/lib/calendar";
import { formatMalaysianDate, formatMalaysianDateTime } from "@/lib/date-format";
import { CheckInFields } from "./CheckInFields";
import { NoShowCorrectionFields } from "./NoShowCorrectionFields";
import { AppointmentStatus } from "./AppointmentStatus";
import { PersonIdentityFacts, VehicleIdentityFacts } from "./IdentityFacts";
import styles from "./StaffWorkspace.module.css";

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
  const [confirming, setConfirming] = useState(false);
  const [ready, setReady] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const correcting = snapshot.data?.status === "NoShow" && actor.role === "BranchManager";
  const controller = useRef<AbortController | null>(null);
  const command = useApiCommand(
    `/api/staff/intake/appointments/${id}/${correcting ? "no-show-corrections" : "check-ins"}`,
    parseCheckIn,
    (receipt, request) =>
      receipt.appointmentId === id &&
      (correcting
        ? correctionMatches(receipt, request)
        : receipt.correction === null && checkInMatches(receipt, request)),
    (path, parse, options) =>
      apiFetch(path, parse, options, { parseConflict: parseIntakeConflict }),
  );
  useLoadingProgress("Refreshing intake appointment", refreshing);
  useLoadingProgress(
    correcting ? "Recording attendance correction" : "Checking in vehicle",
    command.busy,
  );
  const value = draft ?? localDraft;
  const locked = command.busy || command.uncertain;
  const dirty = !command.receipt && JSON.stringify(value) !== JSON.stringify(emptyCheckIn);
  const guarded = dirty || locked;
  const guard = useEditGuard({
    dirty,
    pending: command.busy,
    uncertain: command.uncertain,
    label: correcting ? "Attendance correction" : "Physical check-in",
    discard: () => (onDraft ?? setLocalDraft)(emptyCheckIn),
  });
  useLayoutEffect(() => {
    onLock?.(guarded);
    return () => onLock?.(false);
  }, [guarded, onLock]);
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
      const result = await apiFetch(`/api/staff/intake/appointments/${id}`, parseDetail, {
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
    guard(true);
    const result = await command.submit({
      ...value,
      mileageKilometres: Number(value.mileageKilometres),
      ...(correcting && snapshot.data
        ? {
            attendedAt: `${calendarDate(new Date(snapshot.data.startsAt))}T${value.attendedTime}:00+08:00`,
            reason: value.correctionReason,
          }
        : {}),
    });
    if (result?.status !== 200 || !result.data) {
      return;
    }
    const receipt = result.data;
    setSnapshot((current) => ({
      ...current,
      data: current.data
        ? {
            ...current.data,
            status: "CheckedIn",
            attendanceCorrectedAt: receipt.correction?.recordedAt ?? null,
            correctionReason: receipt.correction?.reason ?? null,
            correctedByEmail: null,
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
      <ConfirmationDialog
        open={confirming}
        title={correcting ? "Correct this attendance record?" : "Check in this vehicle?"}
        confirmLabel={correcting ? "Confirm attendance correction" : "Confirm check-in"}
        busy={command.busy}
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          await checkIn();
          setConfirming(false);
        }}
      >
        <p>
          {correcting
            ? "Replace the no-show status with the confirmed attendance and correction reason."
            : "Record the physical arrival and create the vehicle's inspection record."}
          {detail && ` Vehicle: ${detail.registrationPlate}.`}
        </p>
      </ConfirmationDialog>
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
            <PersonIdentityFacts
              role="Customer"
              name={detail.customerName}
              mobile={detail.customerMobile}
              email={detail.customerEmail}
            />
            <div>
              <dt>Inspection</dt>
              <dd>{formatMalaysianDateTime(detail.startsAt)}</dd>
            </div>
            <VehicleIdentityFacts
              registration={detail.registrationPlate}
              vehicle={`${detail.manufactureYear} ${detail.make} ${detail.model} ${detail.trim}`}
            />
            <div>
              <dt>Declared mileage</dt>
              <dd>{detail.mileageKilometres.toLocaleString("en-MY")} km</dd>
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
              {detail.correctedByEmail && <p>Corrected by {detail.correctedByEmail}.</p>}
            </div>
          )}
          {command.receipt && (
            <div className={styles.stack}>
              <h3>Check-in confirmed</h3>
              <p>{formatMalaysianDateTime(command.receipt.checkedInAt)}</p>
              <p className={styles.help}>
                The vehicle is pending inspection. The booked vehicle details and declared mileage
                are retained.
              </p>
            </div>
          )}
          {actor.role === "GeneralManager" && (
            <p className={styles.help}>Your role has read-only intake access.</p>
          )}
          {detail.status === "NoShow" && actor.role !== "BranchManager" && (
            <p className={styles.help}>
              Only the same-branch BranchManager can correct attendance. Later-date arrivals need a
              new booking.
            </p>
          )}
          {actor.role !== "GeneralManager" &&
            (detail.status === "Booked" || correcting) &&
            !command.receipt && (
              <form
                method="post"
                className={styles.protocol}
                onSubmit={(event) => {
                  event.preventDefault();
                  if (command.uncertain) {
                    void checkIn();
                    return;
                  }
                  setConfirming(true);
                }}
              >
                <h3>{correcting ? "Correct no-show attendance" : "Physical check-in"}</h3>
                <p className={styles.help}>
                  {correcting
                    ? "Record attendance that occurred on"
                    : "Check-in is available only on"}{" "}
                  the booked Malaysian date, {formatMalaysianDate(detail.startsAt)}. Confirm the
                  physical vehicle and customer. The service checks current eligibility.
                </p>
                {correcting && (
                  <NoShowCorrectionFields
                    draft={value}
                    onChange={onDraft ?? setLocalDraft}
                    disabled={!ready || locked || refreshing}
                  />
                )}
                <CheckInFields
                  draft={value}
                  onChange={onDraft ?? setLocalDraft}
                  disabled={!ready || locked || refreshing}
                  retrospective={correcting}
                />
                <div className={styles.actionFooter}>
                  <p className={styles.help}>
                    Record the physical arrival. Booked facts remain unchanged.
                  </p>
                  {dirty && (
                    <ConfirmationButton
                      size="compact"
                      variant="secondary"
                      disabled={locked}
                      confirmationTitle="Discard check-in entries?"
                      message="Your unsaved physical check-in and attendance entries will be cleared."
                      onConfirm={() => (onDraft ?? setLocalDraft)(emptyCheckIn)}
                    >
                      Discard changes
                    </ConfirmationButton>
                  )}
                  <Button
                    size="compact"
                    type="submit"
                    disabled={!ready || command.busy || refreshing}
                  >
                    {commandLabel(correcting, command.uncertain)}
                  </Button>
                </div>
              </form>
            )}
        </>
      )}
    </div>
  );
}

function commandLabel(correcting: boolean, uncertain: boolean) {
  if (uncertain) {
    return correcting ? "Retry same correction" : "Retry same check-in";
  }
  return correcting ? "Confirm attendance correction" : "Confirm check-in";
}

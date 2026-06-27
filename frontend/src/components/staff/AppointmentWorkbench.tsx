"use client";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useEditGuard } from "@/hooks/use-edit-guard";
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
import { formatMalaysianDate } from "@/lib/date-format";
import { CheckInFields } from "./CheckInFields";
import { NoShowCorrectionFields } from "./NoShowCorrectionFields";
import { IntakeRecord } from "./IntakeRecord";
import { IntakeFooter } from "./IntakeFooter";
import { IntakeOwnership } from "./IntakeOwnership";
import { StaffContact } from "./StaffContact";
import record from "./StaffRecordContent.module.css";
import appointment from "./AppointmentWorkbench.module.css";
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
  scrollWithHost = false,
}: {
  id: string;
  actor: CurrentActor;
  initial: ApiResult<AppointmentDetail>;
  actionsContainer?: HTMLElement | null;
  draft?: CheckInDraft;
  onDraft?: (draft: CheckInDraft) => void;
  onLock?: (locked: boolean) => void;
  onConfirmed?: (detail: AppointmentDetail) => void;
  scrollWithHost?: boolean;
}) {
  useInitialToast(initial.message);
  const toast = useToast();
  const [snapshot, setSnapshot] = useState(initial);
  const [localDraft, setLocalDraft] = useState(emptyCheckIn);
  const [confirming, setConfirming] = useState(false);
  const [ready, setReady] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [ownershipGuarded, setOwnershipGuarded] = useState(false);
  const [ownershipActions, setOwnershipActions] = useState<HTMLDivElement | null>(null);
  const formId = `intake-attendance-${id}`;
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
  const guarded = dirty || locked || ownershipGuarded;
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
  const mayCheckIn = Boolean(
    detail?.status === "Booked" &&
    detail.ownership?.handlerId === actor.userId &&
    detail.ownership.handlerEligible,
  );
  const mayRecordAttendance = mayCheckIn || correcting;
  const showForm =
    actor.role !== "GeneralManager" &&
    (mayRecordAttendance || dirty || command.uncertain) &&
    !command.receipt;

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
      return result.data;
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
    if (!command.uncertain && !mayRecordAttendance) {
      return;
    }
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
            correctedByName: null,
          }
        : null,
    }));
    if (snapshot.data) {
      onConfirmed?.({ ...snapshot.data, status: "CheckedIn" });
    }
  }
  const refreshControl = (
    <RefreshButton
      label="Refresh appointment"
      busy={refreshing}
      disabled={!ready || locked || ownershipGuarded}
      onClick={() => void refresh()}
    />
  );

  return (
    <div className={styles.workspace} data-scroll-with-host={scrollWithHost || undefined}>
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
          <IntakeRecord detail={detail} checkedInAt={command.receipt?.checkedInAt} />
          <div className={`${styles.body} ${appointment.body}`}>
            <section className={record.group} aria-label="Customer">
              <h3 className={record.title}>{detail.customerName}</h3>
              <dl className={appointment.contacts}>
                <StaffContact method="phone" value={detail.customerMobile} />
                <StaffContact method="email" value={detail.customerEmail} />
              </dl>
            </section>
            <IntakeOwnership
              actor={actor}
              detail={detail}
              actionsContainer={ownershipActions}
              disabled={!ready || locked || dirty || refreshing}
              onGuarded={setOwnershipGuarded}
              onChanged={async () => {
                const confirmed = await refresh();
                if (confirmed) {
                  onConfirmed?.(confirmed);
                }
              }}
            />
            {detail.status === "NoShow" && actor.role !== "BranchManager" && (
              <p className={styles.help}>
                Only the same-branch BranchManager can correct attendance. Later-date arrivals need
                a new booking.
              </p>
            )}
            {showForm && (
              <form
                id={formId}
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
                {!mayRecordAttendance && !command.uncertain && (
                  <p className={styles.help}>
                    Your entries are retained. Current ownership or appointment status prevents a
                    fresh check-in. Discard these entries before changing ownership.
                  </p>
                )}
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
                    disabled={
                      !ready || locked || ownershipGuarded || refreshing || !mayRecordAttendance
                    }
                  />
                )}
                <CheckInFields
                  draft={value}
                  onChange={onDraft ?? setLocalDraft}
                  disabled={
                    !ready || locked || ownershipGuarded || refreshing || !mayRecordAttendance
                  }
                  retrospective={correcting}
                />
              </form>
            )}
          </div>
          <IntakeFooter
            formId={formId}
            physical={showForm}
            dirty={dirty}
            locked={locked}
            disabled={
              !ready ||
              command.busy ||
              ownershipGuarded ||
              refreshing ||
              (!mayRecordAttendance && !command.uncertain)
            }
            uncertain={command.uncertain}
            correcting={correcting}
            confirmed={Boolean(command.receipt)}
            readOnly={actor.role === "GeneralManager"}
            terminal={detail.status !== "Booked" && !correcting}
            onDiscard={() => (onDraft ?? setLocalDraft)(emptyCheckIn)}
            onOwnershipContainer={setOwnershipActions}
          />
        </>
      )}
    </div>
  );
}

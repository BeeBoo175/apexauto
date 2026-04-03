"use client";

import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { useAppointmentClientEntry } from "@/hooks/use-appointment-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import { StaffLink } from "./StaffLink";
import { StaffCommandBar, StaffLoading, StaffNotice } from "./StaffPage";
import styles from "./Staff.module.css";

export function AppointmentClientEntry({
  id,
  actor,
  returnTo,
}: {
  id: string;
  actor: CurrentActor;
  returnTo: string;
}) {
  const { entry, failed, retry } = useAppointmentClientEntry(id);
  if (failed) {
    return (
      <StaffNotice title="Intake appointment unavailable">
        <p>This appointment is unavailable to this account or could not be confirmed.</p>
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry appointment
        </Button>
        <StaffLink prefetch={false} href={returnTo}>
          Back to intake appointments
        </StaffLink>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Workbench, initial } = entry;
  return (
    <>
      <StaffCommandBar title="Intake appointment" />
      <div className={styles.direct}>
        <StaffLink className={styles.backLink} href={returnTo} prefetch={false}>
          Back to intake appointments
        </StaffLink>
        <Card>
          <Workbench id={id} actor={actor} initial={initial} />
        </Card>
      </div>
    </>
  );
}

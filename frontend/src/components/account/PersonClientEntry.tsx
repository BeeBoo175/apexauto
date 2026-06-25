"use client";

import { PersonProfile } from "@/components/auth/PersonProfile";
import { Button } from "@/components/ui/Button";
import { usePersonClientEntry } from "@/hooks/use-person-client-entry";
import { StaffLink } from "@/components/staff/StaffLink";
import { StaffCommandBar, StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import styles from "@/components/staff/Staff.module.css";

export function PersonClientEntry({ id }: { id: string }) {
  const { entry, failed, retry } = usePersonClientEntry(id);
  if (failed) {
    return (
      <StaffNotice title="Profile unavailable">
        <p>This profile is unavailable to this account or could not be confirmed.</p>
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry profile
        </Button>
        <StaffLink href="/dashboard">Return to your staff workspace</StaffLink>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return (
    <>
      <StaffCommandBar title="Person" />
      <div className={styles.direct}>
        <PersonProfile person={entry} />
      </div>
    </>
  );
}

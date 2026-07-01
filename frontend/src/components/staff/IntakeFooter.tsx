import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { StaffWorkspaceFooter } from "./StaffWorkspaceFooter";
import styles from "./StaffWorkspace.module.css";

export function IntakeFooter({
  formId,
  physical,
  dirty,
  locked,
  disabled,
  uncertain,
  correcting,
  confirmed,
  readOnly,
  terminal,
  onDiscard,
  onOwnershipContainer,
}: {
  formId: string;
  physical: boolean;
  dirty: boolean;
  locked: boolean;
  disabled: boolean;
  uncertain: boolean;
  correcting: boolean;
  confirmed: boolean;
  readOnly: boolean;
  terminal: boolean;
  onDiscard: () => void;
  onOwnershipContainer: (container: HTMLDivElement | null) => void;
}) {
  const context = footerContext({
    physical,
    dirty,
    uncertain,
    correcting,
    confirmed,
    readOnly,
    terminal,
  });
  let action = correcting ? "Confirm attendance correction" : "Confirm check-in";
  if (uncertain) {
    action = correcting ? "Retry same correction" : "Retry same check-in";
  }
  return (
    <StaffWorkspaceFooter title={context.title} description={context.description}>
      <div ref={onOwnershipContainer} className={styles.footerActions} />
      {physical && dirty && (
        <ConfirmationButton
          size="compact"
          variant="secondary"
          disabled={locked}
          confirmationTitle="Discard check-in entries?"
          message="Your unsaved physical check-in and attendance entries will be cleared."
          onConfirm={onDiscard}
        >
          Discard changes
        </ConfirmationButton>
      )}
      {physical && (
        <Button size="compact" type="submit" form={formId} disabled={disabled}>
          {action}
        </Button>
      )}
    </StaffWorkspaceFooter>
  );
}

function footerContext({
  physical,
  dirty,
  uncertain,
  correcting,
  confirmed,
  readOnly,
  terminal,
}: {
  physical: boolean;
  dirty: boolean;
  uncertain: boolean;
  correcting: boolean;
  confirmed: boolean;
  readOnly: boolean;
  terminal: boolean;
}) {
  if (uncertain) {
    return {
      title: "Attendance action unconfirmed",
      description: "Confirm the earlier action first.",
    };
  }
  if (confirmed) {
    return { title: "Check-in confirmed", description: "The vehicle is pending inspection." };
  }
  if (physical) {
    return {
      title: dirty ? "Unsaved attendance entries" : "Physical check-in",
      description: correcting
        ? "Confirm attendance and the reason."
        : "Confirm the customer and vehicle.",
    };
  }
  if (readOnly) {
    return {
      title: "Read-only appointment",
      description: "Your role has read-only access.",
    };
  }
  if (terminal) {
    return { title: "Appointment history", description: "This appointment is read only." };
  }
  return {
    title: "Appointment ownership",
    description: "Claim or assign a handler first.",
  };
}

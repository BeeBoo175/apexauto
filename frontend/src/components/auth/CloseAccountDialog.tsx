"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Input } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import { useAccountClosure } from "@/hooks/use-account-closure";
import type { ClosureBlocker, RetainedCategory } from "@/lib/accounts/customer-closure";
import styles from "./CloseAccountDialog.module.css";

function BlockerList({ blockers }: { blockers: ClosureBlocker[] }) {
  return (
    <>
      <p>Closing is blocked while these records still need you. Nothing is cancelled for you.</p>
      <ul className={styles.list}>
        {blockers.map((blocker) => (
          <li key={`${blocker.type}-${blocker.reference}`}>
            <strong>
              {blocker.type} {blocker.reference}
            </strong>
            <span>
              {blocker.status}
              {blocker.branchName ? `, ${blocker.branchName}` : ""}
            </span>
            <span>{blocker.resolution}</span>
          </li>
        ))}
      </ul>
    </>
  );
}

function RetainedList({ retained }: { retained: RetainedCategory[] }) {
  return (
    <ul className={styles.list}>
      {retained.map((category) => (
        <li key={category.name}>
          <strong>{category.name}</strong>
          <span>{category.detail}</span>
          <span>Kept: {category.endsWhen}</span>
        </li>
      ))}
    </ul>
  );
}

// Closing is permanent: the sign-in identity, contact details and live name are removed, while records the business
// must keep stay with the names listed here. The result is pseudonymized, so the dialog never calls it anonymous.
export function CloseAccountDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const closure = useAccountClosure(open);
  const router = useRouter();
  const [password, setPassword] = useState("");
  const { error } = useToast();
  const { problem, state } = closure;

  useEffect(() => {
    if (problem) {
      error(problem);
    }
  }, [problem, error]);

  useEffect(() => {
    if (open && state.status === "failed") {
      error("Account closure details could not be loaded. Check again to continue.");
    }
  }, [open, state.status, error]);

  function leave() {
    router.replace("/");
  }

  function close() {
    setPassword("");
    closure.clearProblem();
    onClose();
  }

  if (closure.receipt || closure.accessClosed) {
    return (
      <ConfirmationDialog
        open={open}
        title="Account closed"
        confirmLabel="Done"
        cancelLabel="Leave"
        locked
        onClose={leave}
        onConfirm={leave}
      >
        <p>
          {closure.receipt
            ? "Your access has ended and your personal details were removed."
            : "Your access is closed and the erasure is recorded."}
          {closure.receipt?.fileRemovalPending
            ? " Your picture and uploaded files are still being removed."
            : ""}
        </p>
        <p>
          This is pseudonymized, not anonymous: some records keep your details for the reasons
          below.
        </p>
        {closure.receipt && <RetainedList retained={closure.receipt.retained} />}
      </ConfirmationDialog>
    );
  }

  if (state.status !== "ready") {
    return (
      <ConfirmationDialog
        open={open}
        title="Close your account"
        confirmLabel="Check again"
        cancelLabel="Not now"
        busy={state.status === "loading"}
        onClose={close}
        onConfirm={closure.reload}
      >
        <p>
          {state.status === "loading"
            ? "Checking your account."
            : "Your account could not be checked."}
        </p>
      </ConfirmationDialog>
    );
  }

  if (state.preview.blockers.length > 0) {
    return (
      <ConfirmationDialog
        open={open}
        title="Close your account"
        confirmLabel="Check again"
        cancelLabel="Not now"
        onClose={close}
        onConfirm={closure.reload}
      >
        <BlockerList blockers={state.preview.blockers} />
      </ConfirmationDialog>
    );
  }

  return (
    <ConfirmationDialog
      open={open}
      title="Close your account"
      confirmLabel={closure.uncertain ? "Confirm again" : "Close my account permanently"}
      cancelLabel="Not now"
      danger
      busy={closure.busy}
      locked={closure.uncertain}
      onClose={close}
      onConfirm={() => closure.confirm(password)}
    >
      <p>
        <strong>This cannot be undone.</strong> You will be signed out everywhere, your email,
        username, name, NRIC, mobile and address are removed, and nobody can restore the account.
        You can register again later as a new customer.
      </p>
      <p>Not everything is erased. These records stay, for the reasons given:</p>
      <RetainedList retained={state.preview.retained} />
      <Input
        label="Your password"
        type="password"
        value={password}
        autoComplete="current-password"
        maxLength={128}
        disabled={closure.busy || closure.uncertain}
        aria-invalid={problem ? true : undefined}
        onChange={(event) => {
          closure.clearProblem();
          setPassword(event.target.value);
        }}
      />
    </ConfirmationDialog>
  );
}

"use client";

import { useRouter } from "next/navigation";
import { useAccountClosure } from "@/hooks/use-account-closure";
import { ErasureDialog } from "./ErasureDialog";

// Closing is permanent: the sign-in identity, contact details and live name are removed, while records the business
// must keep stay with the names listed here. The result is pseudonymized, so the dialog never calls it anonymous.
export function CloseAccountDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const closure = useAccountClosure(open);
  const router = useRouter();

  function leave() {
    router.replace("/");
  }

  return (
    <ErasureDialog
      open={open}
      erasure={closure}
      title="Close your account"
      checkingText="Checking your account."
      failedText="Your account could not be checked."
      failedToast="Account closure details could not be loaded. Check again to continue."
      warning={
        <p>
          <strong>This cannot be undone.</strong> You will be signed out everywhere and nobody can
          restore the account. Your sign-in and contact details are removed from your account. Your
          name and the details a sale document needs are kept only on the records listed below. You
          can register again later as a new customer.
        </p>
      }
      confirmLabel="Close my account permanently"
      finished={closure.accessClosed}
      finishedTitle="Account closed"
      finishedCancelLabel="Leave"
      finishedBody={
        <>
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
        </>
      }
      onConfirm={closure.confirm}
      onClose={onClose}
      onDone={leave}
    />
  );
}

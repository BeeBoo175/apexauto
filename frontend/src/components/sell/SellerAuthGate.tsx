import { buttonClassName } from "@/components/ui/Button";
import type { SellerSnapshot } from "@/lib/intake/contracts";
import styles from "./Sell.module.css";

// Explains why the photo step is locked and where this visitor can go next: a customer signs in or verifies,
// and a signed-in staff member is sent to the workspace their role may use.
export function SellerAuthGate({
  initial,
  onSignIn,
}: {
  initial: SellerSnapshot;
  onSignIn: () => void;
}) {
  if (initial.staffNotice) {
    return (
      <div className={styles.authGateCard}>
        <p>{initial.staffNotice.message}</p>
        <a href={initial.staffNotice.href} className={buttonClassName("primary", "md")}>
          {initial.staffNotice.label}
        </a>
      </div>
    );
  }
  return (
    <div className={styles.authGateCard}>
      <p>
        {initial.account
          ? "Verify your account email before adding photos or booking an inspection bay."
          : "Sign in with a verified customer account to continue. Your entered vehicle details are kept."}
      </p>
      <a
        href={initial.account ? "/account" : "/login?returnTo=%2Fsell"}
        className={buttonClassName("primary", "md")}
        onClick={onSignIn}
      >
        {initial.account ? "Manage email verification" : "Sign in to continue"}
      </a>
    </div>
  );
}

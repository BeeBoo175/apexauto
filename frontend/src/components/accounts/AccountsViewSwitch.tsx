import { StaffLink } from "@/components/staff/StaffLink";
import { buttonClassName } from "@/components/ui/Button";
import styles from "./StaffAccounts.module.css";

// The accounts page has a Staff view and a Customers view; the command bar switches between them.
export function AccountsViewSwitch({ view }: { view: "staff" | "customers" }) {
  return (
    <nav aria-label="Account views" className={styles.viewSwitch}>
      <StaffLink
        href="/dashboard/accounts"
        prefetch={false}
        className={buttonClassName(view === "staff" ? "secondary" : "shell", "compact")}
        aria-current={view === "staff" ? "page" : undefined}
      >
        Staff
      </StaffLink>
      <StaffLink
        href="/dashboard/accounts?view=customers"
        prefetch={false}
        className={buttonClassName(view === "customers" ? "secondary" : "shell", "compact")}
        aria-current={view === "customers" ? "page" : undefined}
      >
        Customers
      </StaffLink>
    </nav>
  );
}

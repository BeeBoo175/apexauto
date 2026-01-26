import { Button } from "@/components/ui/Button";
import styles from "./AuthForm.module.css";

export interface DemoAccount {
  label: string;
  email: string;
  password: string;
}

export function DemoAccounts({
  accounts,
  onSelect,
}: {
  accounts: readonly DemoAccount[];
  onSelect: (account: DemoAccount) => void;
}) {
  return (
    <details className={styles.demoDetails}>
      <summary className={styles.demoSummary}>
        <span>Demo credentials</span>
        <svg
          className={styles.chevron}
          aria-hidden="true"
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </summary>
      <div className={styles.demoContent}>
        <p className={styles.help}>Choose a role to fill the fields, then select Sign in.</p>
        <div className={styles.demoRoles}>
          {accounts.map((account) => (
            <Button
              key={account.email}
              type="button"
              variant="secondary"
              aria-label={`Use ${account.label} demo account`}
              onClick={() => onSelect(account)}
            >
              {account.label}
            </Button>
          ))}
        </div>
      </div>
    </details>
  );
}

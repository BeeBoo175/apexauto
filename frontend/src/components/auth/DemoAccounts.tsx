import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
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
        <span className={styles.chevron} aria-hidden="true">
          <Icon name="down" />
        </span>
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

import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import styles from "./AuthForm.module.css";

export interface DemoAccount {
  group: string;
  label: string;
  description: string;
  email: string;
  password: string;
}

function groupAccounts(accounts: readonly DemoAccount[]) {
  const groups = new Map<string, DemoAccount[]>();

  for (const account of accounts) {
    groups.set(account.group, [...(groups.get(account.group) ?? []), account]);
  }

  return [...groups.entries()];
}

export function DemoAccounts({
  accounts,
  ready,
  onSelect,
}: {
  accounts: readonly DemoAccount[];
  // The summary is always present so the form never shifts; the accounts appear once filling them can work.
  ready: boolean;
  onSelect: (account: DemoAccount) => void;
}) {
  return (
    <details className={styles.demoDetails}>
      <summary className={styles.demoSummary}>
        <span>Sample credentials</span>
        <span className={styles.chevron} aria-hidden="true">
          <Icon name="down" />
        </span>
      </summary>
      <div className={styles.demoContent}>
        <p className={styles.help}>Choose an account to fill the fields, then select Sign in.</p>
        {ready &&
          groupAccounts(accounts).map(([group, members]) => (
            <section key={group} className={styles.demoGroup}>
              <h3 className={styles.demoGroupTitle}>{group}</h3>
              <div className={styles.demoRoles}>
                {members.map((account) => (
                  <Button
                    key={account.email}
                    type="button"
                    variant="secondary"
                    className={styles.demoOption}
                    aria-label={`Use ${account.label} sample account`}
                    onClick={() => onSelect(account)}
                  >
                    <span className={styles.demoOptionLabel}>{account.label}</span>
                    <span className={styles.demoOptionDescription}>{account.description}</span>
                  </Button>
                ))}
              </div>
            </section>
          ))}
      </div>
    </details>
  );
}

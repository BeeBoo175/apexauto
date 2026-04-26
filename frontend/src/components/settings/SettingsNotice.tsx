import { Button } from "@/components/ui/Button";
import type { SaveNotice } from "@/hooks/use-settings-save";
import styles from "./SettingsForms.module.css";

// A stale write never overwrites silently: the administrator chooses to load the latest record.
export function SettingsNotice({
  notice,
  conflictMessage,
  busy,
  onReload,
  reloadLabel,
  reloadable = true,
}: {
  notice: SaveNotice;
  conflictMessage: string;
  busy: boolean;
  onReload: () => void;
  reloadLabel: string;
  reloadable?: boolean;
}) {
  const conflict = notice.status === 409;
  const reload = reloadable && (conflict || notice.uncertain);
  return (
    <div className={styles.notice} role="alert">
      <p>{conflict ? conflictMessage : notice.message}</p>
      {reload && (
        <Button size="compact" variant="secondary" disabled={busy} onClick={onReload}>
          {reloadLabel}
        </Button>
      )}
    </div>
  );
}

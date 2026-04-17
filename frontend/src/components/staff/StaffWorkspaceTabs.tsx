import { Button } from "@/components/ui/Button";
import type { WorkspaceTab } from "@/hooks/use-workspace-tabs";
import styles from "./StaffWorkspace.module.css";

export function StaffWorkspaceTabs<T extends string>({
  label,
  tabs,
  value,
  disabled = false,
  onChange,
}: {
  label: string;
  tabs: readonly WorkspaceTab<T>[];
  value: T;
  disabled?: boolean;
  onChange: (section: T) => void;
}) {
  if (tabs.length < 2) {
    return null;
  }

  return (
    <nav className={styles.sections} aria-label={label}>
      {tabs.map((tab) => (
        <Button
          key={tab.value}
          size="compact"
          variant="ghost"
          className={styles.sectionButton}
          aria-pressed={value === tab.value}
          disabled={disabled}
          onClick={() => onChange(tab.value)}
        >
          {tab.label}
        </Button>
      ))}
    </nav>
  );
}

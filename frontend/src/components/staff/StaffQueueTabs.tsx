import { Button } from "@/components/ui/Button";
import styles from "./StaffQueueTabs.module.css";

export function StaffQueueTabs<T extends string>({
  label,
  tabs,
  value,
  disabled = false,
  onChange,
}: {
  label: string;
  tabs: readonly { value: T; label: string }[];
  value: T;
  disabled?: boolean;
  onChange: (value: T) => void;
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

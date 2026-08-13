import type { ReactNode } from "react";
import { SearchField } from "@/components/ui/SearchField";
import styles from "./StaffQueueWorkspace.module.css";

export function StaffQueueSearch({
  label,
  placeholder,
  value,
  busy,
  disabled = false,
  onChange,
  onSubmit,
  onClear,
  action,
}: {
  label: string;
  placeholder: string;
  value: string;
  busy: boolean;
  disabled?: boolean;
  onChange: (value: string) => void;
  onSubmit: () => void;
  onClear?: () => void;
  // The queue's create control, beside the search.
  action?: ReactNode;
}) {
  return (
    <div className={styles.search}>
      <div className={styles.searchRow}>
        <form
          className={styles.searchForm}
          onSubmit={(event) => {
            event.preventDefault();
            onSubmit();
          }}
        >
          <SearchField
            label={label}
            placeholder={placeholder}
            value={value}
            disabled={disabled}
            busy={busy}
            onChange={onChange}
            onClear={onClear}
          />
        </form>
        {/* Outside the form: the action opens a dialog that holds forms of its own. */}
        {action}
      </div>
    </div>
  );
}

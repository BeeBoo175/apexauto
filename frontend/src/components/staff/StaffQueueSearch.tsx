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
}: {
  label: string;
  placeholder: string;
  value: string;
  busy: boolean;
  disabled?: boolean;
  onChange: (value: string) => void;
  onSubmit: () => void;
  onClear?: () => void;
}) {
  return (
    <form
      className={styles.search}
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
  );
}

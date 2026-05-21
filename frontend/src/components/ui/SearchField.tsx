import { Button } from "./Button";
import { Icon } from "./Icon";
import { Input } from "./Input";
import styles from "./SearchField.module.css";

export function SearchField({
  label,
  value,
  disabled,
  onChange,
  onClear,
  placeholder,
  busy = false,
  disabledReason,
  density = "compact",
}: {
  label: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
  onClear?: () => void;
  placeholder: string;
  busy?: boolean;
  disabledReason?: string;
  density?: "default" | "compact";
}) {
  return (
    <div className={styles.field}>
      <Input
        label={label}
        labelHidden
        density={density}
        type="search"
        maxLength={160}
        placeholder={placeholder}
        value={value}
        disabled={disabled}
        title={disabledReason}
        onChange={(event) => onChange(event.target.value)}
      />
      {onClear && (
        <Button
          type="button"
          size="icon"
          variant="ghost"
          aria-label="Clear search"
          title={disabledReason ?? "Clear search"}
          disabled={disabled || busy}
          onClick={onClear}
        >
          <Icon name="close" />
        </Button>
      )}
      <Button
        type="submit"
        size="icon"
        variant="ghost"
        aria-label="Search"
        title={disabledReason ?? "Search"}
        disabled={disabled || busy}
      >
        <span className={styles.searchIcon}>
          <Icon name="search" />
        </span>
      </Button>
    </div>
  );
}

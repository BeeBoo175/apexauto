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
}: {
  label: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
  onClear?: () => void;
  placeholder: string;
}) {
  return (
    <div className={styles.field}>
      <Input
        label={label}
        labelHidden
        density="compact"
        type="search"
        maxLength={160}
        placeholder={placeholder}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
      {onClear && (
        <Button
          type="button"
          size="icon"
          variant="ghost"
          aria-label="Clear search"
          title="Clear search"
          disabled={disabled}
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
        title="Search"
        disabled={disabled}
      >
        <span className={styles.searchIcon}>
          <Icon name="search" />
        </span>
      </Button>
    </div>
  );
}

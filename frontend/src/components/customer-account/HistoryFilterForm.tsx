import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { SearchField } from "@/components/ui/SearchField";
import styles from "./HistoryFilterForm.module.css";

interface Draft {
  from: string;
  to: string;
  search?: string;
}

// One filter bar for every account history list: optional search, a Malaysia date range and an apply action.
export function HistoryFilterForm<T extends Draft>({
  draft,
  disabled,
  busy,
  searchLabel,
  searchPlaceholder,
  onDraft,
  onApply,
  onClearSearch,
}: {
  draft: T;
  disabled: boolean;
  busy: boolean;
  searchLabel?: string;
  searchPlaceholder?: string;
  onDraft: (draft: T) => void;
  onApply: () => void;
  onClearSearch?: () => void;
}) {
  const hasSearch =
    draft.search !== undefined && Boolean(searchLabel) && Boolean(searchPlaceholder);

  return (
    <form
      method="post"
      className={`${styles.form} ${hasSearch ? "" : styles.datesOnly}`.trim()}
      onSubmit={(event) => {
        event.preventDefault();
        onApply();
      }}
    >
      {draft.search !== undefined && searchLabel && searchPlaceholder && (
        <div className={styles.search}>
          <SearchField
            label={searchLabel}
            placeholder={searchPlaceholder}
            value={draft.search}
            disabled={disabled}
            busy={busy}
            density="default"
            size="large"
            onChange={(search) => onDraft({ ...draft, search })}
            onClear={draft.search ? onClearSearch : undefined}
          />
        </div>
      )}
      <Input
        label="From (Malaysia)"
        type="date"
        value={draft.from}
        disabled={disabled}
        onChange={(event) => onDraft({ ...draft, from: event.target.value })}
      />
      <Input
        label="To (Malaysia)"
        type="date"
        value={draft.to}
        disabled={disabled}
        onChange={(event) => onDraft({ ...draft, to: event.target.value })}
      />
      <Button variant="secondary" size="md" type="submit" disabled={disabled}>
        {draft.search === undefined ? "Apply dates" : "Apply filters"}
      </Button>
    </form>
  );
}

import { useId, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { parseMyr } from "@/lib/currency-input";
import { dateRangeError } from "@/lib/intake/list-query";
import styles from "./ColumnFilterForm.module.css";

export function ColumnFilterForm({
  children,
  busy,
  onApply,
  onClear,
}: {
  children: ReactNode;
  busy: boolean;
  onApply: () => Promise<boolean | undefined>;
  onClear: () => Promise<boolean | undefined>;
}) {
  const [pending, setPending] = useState(false);

  async function submit(form: HTMLFormElement, action: () => Promise<boolean | undefined>) {
    setPending(true);
    try {
      if (await action()) {
        form.closest<HTMLElement>("[popover]")?.hidePopover();
      }
    } finally {
      setPending(false);
    }
  }

  return (
    <form
      className={styles.form}
      onSubmit={(event) => {
        event.preventDefault();
        void submit(event.currentTarget, onApply);
      }}
    >
      {children}
      <div className={styles.actions}>
        <Button
          size="compact"
          variant="secondary"
          type="button"
          disabled={busy || pending}
          onClick={(event) => {
            const form = event.currentTarget.form;
            if (form) {
              void submit(form, onClear);
            }
          }}
        >
          Clear
        </Button>
        <Button size="compact" type="submit" disabled={busy || pending}>
          Apply
        </Button>
      </div>
    </form>
  );
}

export function MoneyRangeFilter({
  minimum,
  maximum,
  busy,
  onApply,
}: {
  minimum: string;
  maximum: string;
  busy: boolean;
  onApply: (minimum: string, maximum: string) => Promise<boolean | undefined>;
}) {
  const id = useId();
  const [lower, setLower] = useState(minimum);
  const [upper, setUpper] = useState(maximum);
  const [error, setError] = useState("");

  async function apply() {
    const low = lower ? parseMyr(lower) : null;
    const high = upper ? parseMyr(upper) : null;
    const invalidMinimum = Boolean(lower) && low === null;
    const invalidMaximum = Boolean(upper) && high === null;
    const invalidOrder = low !== null && high !== null && low > high;
    if (invalidMinimum || invalidMaximum || invalidOrder) {
      setError("Enter valid MYR amounts with the minimum no greater than the maximum.");
      return false;
    }
    setError("");
    return onApply(lower, upper);
  }

  return (
    <ColumnFilterForm busy={busy} onApply={apply} onClear={() => onApply("", "")}>
      <div className={styles.range}>
        <Input
          id={`${id}-min`}
          label="Minimum (MYR)"
          placeholder="No minimum"
          density="compact"
          inputMode="decimal"
          value={lower}
          disabled={busy}
          onChange={(event) => setLower(event.target.value)}
        />
        <Input
          id={`${id}-max`}
          label="Maximum (MYR)"
          placeholder="No maximum"
          density="compact"
          inputMode="decimal"
          value={upper}
          disabled={busy}
          onChange={(event) => setUpper(event.target.value)}
        />
      </div>
      {error && (
        <p role="alert" className={styles.error}>
          {error}
        </p>
      )}
    </ColumnFilterForm>
  );
}

export function DateRangeFilter({
  from,
  to,
  busy,
  onApply,
}: {
  from: string;
  to: string;
  busy: boolean;
  onApply: (from: string, to: string) => Promise<boolean | undefined>;
}) {
  const id = useId();
  const [first, setFirst] = useState(from);
  const [last, setLast] = useState(to);
  const [error, setError] = useState("");
  async function apply() {
    const invalid = dateRangeError({ from: first, to: last });
    setError(invalid);
    if (invalid) {
      return false;
    }
    return onApply(first, last);
  }
  return (
    <ColumnFilterForm busy={busy} onApply={apply} onClear={() => onApply("", "")}>
      <div className={styles.range}>
        <Input
          id={`${id}-from`}
          label="From"
          type="date"
          density="compact"
          value={first}
          disabled={busy}
          onChange={(event) => setFirst(event.target.value)}
        />
        <Input
          id={`${id}-to`}
          label="To"
          type="date"
          density="compact"
          value={last}
          disabled={busy}
          onChange={(event) => setLast(event.target.value)}
        />
      </div>
      <p className={styles.help}>Malaysia time · inclusive dates</p>
      {error && (
        <p role="alert" className={styles.error}>
          {error}
        </p>
      )}
    </ColumnFilterForm>
  );
}

export function TextColumnFilter({
  label,
  value,
  busy,
  onApply,
}: {
  label: string;
  value: string;
  busy: boolean;
  onApply: (value: string) => Promise<boolean | undefined>;
}) {
  const id = useId();
  const [draft, setDraft] = useState(value);
  return (
    <ColumnFilterForm busy={busy} onApply={() => onApply(draft.trim())} onClear={() => onApply("")}>
      <Input
        id={id}
        label={label}
        labelHidden
        placeholder={`Enter ${label.toLowerCase().replace(/ contains$/, "")}…`}
        density="compact"
        maxLength={160}
        value={draft}
        disabled={busy}
        onChange={(event) => setDraft(event.target.value)}
      />
    </ColumnFilterForm>
  );
}

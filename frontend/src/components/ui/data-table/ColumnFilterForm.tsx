import { useId, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { validateHoursRange, validateMoneyRange } from "@/lib/table-filter-ranges";
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

interface RangeFilterProps {
  minimum: string;
  maximum: string;
  busy: boolean;
  onApply: (minimum: string, maximum: string) => Promise<boolean | undefined>;
}

// One numeric range frame: the caller supplies the unit label and a validator that returns the values to apply
// or the message to show inline, so every range filter reports a bad entry the same way.
function NumericRangeFilter({
  minimum,
  maximum,
  busy,
  unit,
  validate,
  onApply,
}: RangeFilterProps & {
  unit: string;
  validate: (lower: string, upper: string) => { error: string } | { lower: string; upper: string };
}) {
  const id = useId();
  const [lower, setLower] = useState(minimum);
  const [upper, setUpper] = useState(maximum);
  const [error, setError] = useState("");

  async function apply() {
    const checked = validate(lower, upper);
    if ("error" in checked) {
      setError(checked.error);
      return false;
    }
    setError("");
    return onApply(checked.lower, checked.upper);
  }

  return (
    <ColumnFilterForm busy={busy} onApply={apply} onClear={() => onApply("", "")}>
      <div className={styles.range}>
        <Input
          id={`${id}-min`}
          label={`Minimum (${unit})`}
          aria-invalid={error ? true : undefined}
          placeholder="No minimum"
          density="compact"
          inputMode="decimal"
          value={lower}
          disabled={busy}
          onChange={(event) => setLower(event.target.value)}
        />
        <Input
          id={`${id}-max`}
          label={`Maximum (${unit})`}
          aria-invalid={error ? true : undefined}
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

export function MoneyRangeFilter(props: RangeFilterProps) {
  return <NumericRangeFilter {...props} unit="RM" validate={validateMoneyRange} />;
}

function validateWholeRange(lower: string, upper: string) {
  const first = lower.trim();
  const last = upper.trim();
  const invalidValue = [first, last].some((value) => value !== "" && !/^[1-9]\d{0,8}$/.test(value));
  if (invalidValue || Boolean(first && last && Number(first) > Number(last))) {
    return { error: "Enter whole numbers with the minimum no greater than the maximum." };
  }
  return { lower: first, upper: last };
}

// A range of whole numbers in the given unit, such as months or kilometres.
export function WholeRangeFilter({ unit, ...props }: RangeFilterProps & { unit: string }) {
  return <NumericRangeFilter {...props} unit={unit} validate={validateWholeRange} />;
}

export function HoursRangeFilter({
  maximumHours,
  ...props
}: RangeFilterProps & { maximumHours: number }) {
  return (
    <NumericRangeFilter
      {...props}
      unit="hours"
      validate={(lower, upper) => validateHoursRange(lower, upper, maximumHours)}
    />
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

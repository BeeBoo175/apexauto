import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input, Select } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import { validateHoursRange, validateMoneyRange } from "@/lib/table-filter-ranges";
import {
  tableOutcomes,
  tableViews,
  type SalesTableFilters,
  type TableView,
} from "@/lib/sales/contracts";
import { defaultTableFilters, tableViewFilters } from "@/lib/sales/query";
import { saleTextFilters, type LoadSales } from "./sale-filters";
import styles from "./SalesFilterSheet.module.css";

const ranges = [
  { label: "Discount (RM)", minimum: "minimumDiscount", maximum: "maximumDiscount", hours: false },
  {
    label: "Balance payable (RM)",
    minimum: "minimumBalance",
    maximum: "maximumBalance",
    hours: false,
  },
  {
    label: "Time left (hours)",
    minimum: "minimumHoursLeft",
    maximum: "maximumHoursLeft",
    hours: true,
  },
] as const;

export function SalesFilterSheet({
  filters,
  showBranch,
  busy,
  load,
  onClose,
}: {
  filters: SalesTableFilters;
  showBranch: boolean;
  busy: boolean;
  load: LoadSales;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState(filters);
  const [pending, setPending] = useState(false);
  const [closing, setClosing] = useState(false);
  const [invalidRange, setInvalidRange] = useState("");
  const toast = useToast();
  const disabled = busy || pending;

  function edit(key: keyof SalesTableFilters, value: string) {
    setInvalidRange("");
    setDraft((current) => ({ ...current, [key]: value }));
  }

  async function apply() {
    for (const range of ranges) {
      const lower = draft[range.minimum].trim();
      const upper = draft[range.maximum].trim();
      const checked = range.hours
        ? validateHoursRange(lower, upper, 168)
        : validateMoneyRange(lower, upper);
      if ("error" in checked) {
        setInvalidRange(range.minimum);
        toast.error(`${range.label}: ${checked.error}`);
        return;
      }
    }
    setPending(true);
    setInvalidRange("");
    try {
      if (await load(draft, 1)) {
        setClosing(true);
      }
    } finally {
      setPending(false);
    }
  }

  // Clearing applies at once: the sheet has nothing left to edit, so it closes on the unfiltered list.
  async function clear() {
    setPending(true);
    setInvalidRange("");
    try {
      const cleared = {
        ...defaultTableFilters(),
        view: filters.view,
        search: filters.search,
        pageSize: filters.pageSize,
        sort: filters.sort,
        direction: filters.direction,
      };
      if (await load(cleared, 1)) {
        setClosing(true);
      }
    } finally {
      setPending(false);
    }
  }

  function changeStatus(value: string) {
    const outcome = tableOutcomes.find((item) => item === value) ?? "";
    const view = outcome ? "History" : (value as TableView);
    setDraft({ ...tableViewFilters(draft, view), outcome });
  }

  return (
    <Dialog
      open
      title="Filter sales"
      variant="sheet"
      closing={closing}
      onClose={() => setClosing(true)}
      onClosed={onClose}
      closeDisabled={pending}
    >
      <form
        className={styles.form}
        onSubmit={(event) => {
          event.preventDefault();
          if (!disabled) {
            void apply();
          }
        }}
      >
        <div className={styles.fields}>
          <Select
            label="Queue status"
            density="compact"
            value={draft.outcome || draft.view}
            disabled={disabled}
            onChange={(event) => changeStatus(event.target.value)}
          >
            {tableViews.map((view) => (
              <option key={view.value} value={view.value}>
                {view.label}
              </option>
            ))}
            {tableOutcomes.map((outcome) => (
              <option key={outcome} value={outcome}>
                {outcome}
              </option>
            ))}
          </Select>
          {saleTextFilters
            .filter(([key]) => showBranch || key !== "branch")
            .map(([key, label]) => (
              <Input
                key={key}
                label={`${label} contains`}
                density="compact"
                maxLength={160}
                value={draft[key]}
                disabled={disabled}
                onChange={(event) => edit(key, event.target.value)}
              />
            ))}
          {ranges.map((range) => (
            <fieldset key={range.minimum} className={styles.range}>
              <legend>{range.label}</legend>
              <div className={styles.bounds}>
                <Input
                  label={`Minimum ${range.label.toLowerCase()}`}
                  labelHidden
                  placeholder="No minimum"
                  density="compact"
                  inputMode="decimal"
                  value={draft[range.minimum]}
                  aria-invalid={invalidRange === range.minimum || undefined}
                  disabled={disabled}
                  onChange={(event) => edit(range.minimum, event.target.value)}
                />
                <Input
                  label={`Maximum ${range.label.toLowerCase()}`}
                  labelHidden
                  placeholder="No maximum"
                  density="compact"
                  inputMode="decimal"
                  value={draft[range.maximum]}
                  aria-invalid={invalidRange === range.minimum || undefined}
                  disabled={disabled}
                  onChange={(event) => edit(range.maximum, event.target.value)}
                />
              </div>
            </fieldset>
          ))}
        </div>
        <div className={styles.footer}>
          <Button
            type="button"
            variant="ghost"
            size="compact"
            disabled={disabled}
            onClick={() => void clear()}
          >
            Clear all
          </Button>
          <Button
            type="button"
            variant="secondary"
            size="compact"
            disabled={pending}
            onClick={() => setClosing(true)}
          >
            Cancel
          </Button>
          <Button type="submit" size="compact" disabled={disabled}>
            Apply filters
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

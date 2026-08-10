import { useId } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { useSellerAppointment } from "@/hooks/use-seller-appointment";
import styles from "./Sell.module.css";

export function AppointmentFields({
  selection,
  disabled,
}: {
  selection: ReturnType<typeof useSellerAppointment>;
  disabled: boolean;
}) {
  const { branches, offer, selectedBranch, dateWindow, dateError } = selection;
  const dateHelpId = useId();
  const dateErrorId = useId();

  return (
    <fieldset disabled={disabled} className={styles.stack}>
      <Select
        label="Preferred branch"
        id="preferred-branch"
        value={selection.branchId}
        required
        onChange={(event) => selection.selectBranch(event.target.value)}
      >
        <option value="">Choose a branch</option>
        {selectedBranch && !branches.items.some((branch) => branch.id === selectedBranch.id) && (
          <option value={selectedBranch.id}>
            {selectedBranch.name} — {selectedBranch.city}
          </option>
        )}
        {branches.items.map((branch) => (
          <option key={branch.id} value={branch.id}>
            {branch.name} — {branch.city}
          </option>
        ))}
      </Select>
      {branches.total > branches.pageSize && (
        <div className={styles.actions}>
          <Button
            type="button"
            variant="secondary"
            disabled={selection.busy || branches.page === 1}
            onClick={() => void selection.loadBranches(branches.page - 1)}
          >
            Previous branches
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={selection.busy || branches.page * branches.pageSize >= branches.total}
            onClick={() => void selection.loadBranches(branches.page + 1)}
          >
            More branches
          </Button>
        </div>
      )}
      <div className={styles.dateFieldGroup}>
        <Input
          label="Appointment date (Malaysia)"
          type="date"
          required
          value={selection.date}
          min={dateWindow.firstDate}
          max={dateWindow.lastDate}
          aria-invalid={Boolean(dateError) || undefined}
          aria-describedby={dateError ? `${dateErrorId} ${dateHelpId}` : dateHelpId}
          onChange={(event) => selection.selectDate(event.target.value)}
        />
        {dateError && (
          <p id={dateErrorId} className={styles.fieldError} role="alert">
            {dateError}
          </p>
        )}
        <p id={dateHelpId} className={styles.help}>
          Open Monday to Saturday, 09:00–18:00 MYT, within the next 30 days (2 hours’ notice).
        </p>
      </div>
      <Button
        type="button"
        variant="secondary"
        disabled={selection.busy || !selection.branchId || !selection.date || Boolean(dateError)}
        onClick={() => void selection.loadSlots()}
      >
        {selection.busy ? "Checking slots" : "Check available slots"}
      </Button>
      {offer && (
        <fieldset className={styles.stack}>
          <legend>Available inspection times</legend>
          <p className={styles.help}>Offers reserve nothing. Times are in Malaysia (UTC+8).</p>
          <div className={styles.slots}>
            {offer.slots.map((slot) => (
              <label key={slot.startsAt}>
                <input
                  type="radio"
                  name="inspection-slot"
                  required
                  value={slot.startsAt}
                  checked={selection.startsAt === slot.startsAt}
                  onChange={() => selection.setStartsAt(slot.startsAt)}
                />
                {formatMalaysianDateTime(slot.startsAt, {
                  year: undefined,
                  month: undefined,
                  day: undefined,
                })}
              </label>
            ))}
          </div>
          {offer.slots.length === 0 && (
            <p>No slots are available for this date. Choose another date.</p>
          )}
        </fieldset>
      )}
    </fieldset>
  );
}

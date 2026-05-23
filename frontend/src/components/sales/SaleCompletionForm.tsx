"use client";

import { useId, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Input, Select } from "@/components/ui/Input";
import type { useSaleCompletion } from "@/hooks/use-sale-completion";
import type { PaymentMethod } from "@/lib/sales/completion-evidence";
import { formatCurrency } from "@/lib/currency-format";
import forms from "@/components/staff/StaffForms.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import record from "@/components/staff/StaffRecordContent.module.css";
import styles from "./SaleCompletion.module.css";

export function SaleCompletionForm({
  workflow,
  balance,
  actionsContainer,
  active,
}: {
  workflow: ReturnType<typeof useSaleCompletion>;
  balance: number;
  actionsContainer: HTMLElement | null;
  active: boolean;
}) {
  const formId = useId();
  const [confirming, setConfirming] = useState(false);
  const { stage, review, payment, handover } = workflow;
  if (!stage) {
    return null;
  }
  const disabled = workflow.blocked || workflow.uncertain;
  const settlement = stage === "settlement";
  const action = settlement ? "Confirm settlement" : "Complete handover";
  const fields = settlement ? (
    <>
      {review && (
        <section className={forms.section}>
          <h3>Review buyer and vehicle</h3>
          <Input
            label="Buyer name"
            density="compact"
            maxLength={200}
            value={review.buyerName}
            required
            disabled={disabled}
            onChange={(event) => workflow.changeReview({ buyerName: event.target.value })}
          />
          <Input
            label="Buyer address"
            density="compact"
            maxLength={1000}
            value={review.buyerAddress}
            required
            disabled={disabled}
            onChange={(event) => workflow.changeReview({ buyerAddress: event.target.value })}
          />
          <dl className={record.facts}>
            <div>
              <dt>NRIC</dt>
              <dd>{review.buyerNric}</dd>
            </div>
            <div>
              <dt>Vehicle</dt>
              <dd>
                {review.vehicle.manufactureYear} {review.vehicle.make} {review.vehicle.model}{" "}
                {review.vehicle.trim}
              </dd>
            </div>
            <div>
              <dt>Registration / VIN</dt>
              <dd>
                {review.vehicle.registrationPlate} / {review.vehicle.vin}
              </dd>
            </div>
            <div>
              <dt>Recorded mileage</dt>
              <dd>{review.vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
            </div>
          </dl>
          <label className={forms.check}>
            <input
              type="checkbox"
              checked={workflow.reviewed}
              disabled={disabled}
              onChange={(event) => workflow.setReviewed(event.target.checked)}
            />
            I have reviewed these buyer and vehicle details for the issued documents
          </label>
        </section>
      )}
      <section className={forms.section}>
        <h3>Payment verification</h3>
        <p className={forms.hint}>
          Approved balance {formatCurrency(balance)} already includes the deposit and trade-in
          credit.
        </p>
        {balance !== 0 && (
          <Select
            label="Payment method"
            density="compact"
            value={payment.method}
            disabled={disabled}
            onChange={(event) =>
              workflow.changePayment({ method: event.target.value as PaymentMethod })
            }
          >
            <option value="Cash">Cash</option>
            <option value="BankTransfer">Bank transfer</option>
            <option value="HirePurchase">Hire purchase</option>
          </Select>
        )}
        {payment.method === "ZeroBalance" ? (
          <label className={forms.check}>
            <input
              type="checkbox"
              checked={payment.zeroConfirmed}
              disabled={disabled}
              onChange={(event) => workflow.changePayment({ zeroConfirmed: event.target.checked })}
            />
            I confirm no further payment is required
          </label>
        ) : (
          <Input
            label={
              payment.method === "HirePurchase"
                ? "Received customer contribution (MYR)"
                : "Received amount (MYR)"
            }
            density="compact"
            inputMode="decimal"
            required
            value={payment.received}
            disabled={disabled}
            onChange={(event) => workflow.changePayment({ received: event.target.value })}
          />
        )}
        {(payment.method === "Cash" || payment.method === "BankTransfer") && (
          <>
            <Input
              label="Payment reference"
              density="compact"
              required
              maxLength={1000}
              value={payment.reference}
              disabled={disabled}
              onChange={(event) => workflow.changePayment({ reference: event.target.value })}
            />
            <Input
              label="Payment date"
              type="date"
              density="compact"
              required
              value={payment.date}
              disabled={disabled}
              onChange={(event) => workflow.changePayment({ date: event.target.value })}
            />
          </>
        )}
        {payment.method === "HirePurchase" && (
          <>
            <Input
              label="Financing bank"
              density="compact"
              required
              maxLength={100}
              value={payment.bank}
              disabled={disabled}
              onChange={(event) => workflow.changePayment({ bank: event.target.value })}
            />
            <Input
              label="Financing amount (MYR)"
              density="compact"
              inputMode="decimal"
              required
              value={payment.financing}
              disabled={disabled}
              onChange={(event) => workflow.changePayment({ financing: event.target.value })}
            />
            <Input
              label="Letter of Undertaking reference"
              density="compact"
              required
              maxLength={1000}
              value={payment.lou}
              disabled={disabled}
              onChange={(event) => workflow.changePayment({ lou: event.target.value })}
            />
            <label className={forms.check}>
              <input
                type="checkbox"
                checked={payment.louVerified}
                disabled={disabled}
                onChange={(event) => workflow.changePayment({ louVerified: event.target.checked })}
              />
              I have verified the Letter of Undertaking
            </label>
          </>
        )}
      </section>
    </>
  ) : (
    <section className={forms.section}>
      <h3>Physical handover</h3>
      <div className={forms.pair}>
        <Input
          label="Final odometer (km)"
          density="compact"
          inputMode="numeric"
          required
          value={handover.odometer}
          disabled={disabled}
          onChange={(event) => workflow.changeHandover({ odometer: event.target.value })}
        />
        <Input
          label="Keys delivered"
          density="compact"
          inputMode="numeric"
          required
          value={handover.keys}
          disabled={disabled}
          onChange={(event) => workflow.changeHandover({ keys: event.target.value })}
        />
      </div>
      <h3>Delivery confirmations</h3>
      <label className={forms.check}>
        <input
          type="checkbox"
          checked={handover.customer}
          disabled={disabled}
          onChange={(event) => workflow.changeHandover({ customer: event.target.checked })}
        />
        Customer acknowledges receipt of the vehicle
      </label>
      <label className={forms.check}>
        <input
          type="checkbox"
          checked={handover.ownership}
          disabled={disabled}
          onChange={(event) => workflow.changeHandover({ ownership: event.target.checked })}
        />
        Ownership transfer is complete
      </label>
      <label className={forms.check}>
        <input
          type="checkbox"
          checked={handover.insurance}
          disabled={disabled}
          onChange={(event) => workflow.changeHandover({ insurance: event.target.checked })}
        />
        Insurance is confirmed
      </label>
    </section>
  );
  const actions = (
    <>
      <ConfirmationButton
        size="compact"
        variant="secondary"
        disabled={disabled}
        confirmationTitle="Discard these entries?"
        message="Your unsent settlement or handover entries will be cleared."
        onConfirm={workflow.discard}
      >
        Discard changes
      </ConfirmationButton>
      <Button
        type="submit"
        form={formId}
        size="compact"
        disabled={workflow.blocked || (!workflow.uncertain && (!workflow.valid || workflow.stale))}
      >
        {workflow.uncertain ? `Reconcile ${stage}` : action}
      </Button>
    </>
  );
  return (
    <form
      id={formId}
      className={styles.form}
      onSubmit={(event) => {
        event.preventDefault();
        if (workflow.blocked || (!workflow.uncertain && (!workflow.valid || workflow.stale))) {
          return;
        }
        if (workflow.uncertain) {
          void workflow.submit();
        } else {
          setConfirming(true);
        }
      }}
    >
      <div className={settlement ? styles.columns : forms.section}>{fields}</div>
      {workflow.stale && (
        <p className={forms.hint}>
          The sale changed after this review. Discard these entries and start a fresh review before
          confirming.
        </p>
      )}
      <ConfirmationDialog
        open={confirming}
        title={`${action}?`}
        confirmLabel={action}
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          setConfirming(false);
          await workflow.submit();
        }}
      >
        <p>
          {settlement
            ? "This freezes the approved terms, records the payment evidence and issues the purchase order and receipt. The sale can no longer be cancelled."
            : "This records delivery and issues the handover note. A confirmation email is queued for the customer."}
        </p>
      </ConfirmationDialog>
      {active &&
        (actionsContainer ? (
          createPortal(actions, actionsContainer)
        ) : (
          <div className={workbench.footerActions}>{actions}</div>
        ))}
    </form>
  );
}

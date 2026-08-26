"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { useDepositRefundRequest } from "@/hooks/use-deposit-refund-request";
import { useDepositRefunds } from "@/hooks/use-deposit-refunds";
import { formatCurrency } from "@/lib/currency-format";
import type { DepositRefund, RefundBank } from "@/lib/vehicles/account-refunds";
import styles from "./DepositRefunds.module.css";

function reasonText(refund: DepositRefund) {
  return refund.reason === "released"
    ? "Your reservation was released, so the deposit is yours again."
    : "Your payment arrived after the reservation could be made, so the deposit is yours again.";
}

function submitLabel(refund: DepositRefund, busy: boolean, uncertain: boolean) {
  if (busy) {
    return "Requesting";
  }

  if (uncertain) {
    return "Check again";
  }

  return refund.state === "failed" ? "Try again" : "Refund to my account";
}

function RefundForm({
  refund,
  banks,
  onSettled,
}: {
  refund: DepositRefund;
  banks: RefundBank[];
  onSettled: () => void;
}) {
  const request = useDepositRefundRequest(refund.paymentRequestId, onSettled);
  const busy = request.busy;
  const [bankCode, setBankCode] = useState("");
  const [accountNumber, setAccountNumber] = useState("");
  const [accountHolder, setAccountHolder] = useState("");
  const ready = bankCode && /^[0-9]{6,20}$/.test(accountNumber) && accountHolder.trim();

  return (
    <form
      className={styles.form}
      onSubmit={(event) => {
        event.preventDefault();
        if (ready) {
          void request.submit({ bankCode, accountNumber, accountHolder: accountHolder.trim() });
        }
      }}
    >
      <Select
        label="Bank"
        value={bankCode}
        onChange={(event) => setBankCode(event.target.value)}
        disabled={busy}
      >
        <option value="">Choose your bank</option>
        {banks.map((bank) => (
          <option key={bank.code} value={bank.code}>
            {bank.name}
          </option>
        ))}
      </Select>
      <Input
        label="Account number"
        inputMode="numeric"
        autoComplete="off"
        value={accountNumber}
        onChange={(event) => setAccountNumber(event.target.value.replace(/\D/g, ""))}
        maxLength={20}
        disabled={busy}
      />
      <Input
        label="Account holder name"
        autoComplete="off"
        value={accountHolder}
        onChange={(event) => setAccountHolder(event.target.value)}
        maxLength={100}
        disabled={busy}
      />
      <p className={styles.note}>
        We send the account number to our payment provider and do not keep it. Only the bank and the
        last four digits stay on your account.
      </p>
      <Button type="submit" disabled={!ready || busy}>
        {submitLabel(refund, busy, request.uncertain)}
      </Button>
    </form>
  );
}

export function DepositRefunds({ actorId }: { actorId: string }) {
  const refunds = useDepositRefunds(actorId);
  const list = refunds.list;

  if (refunds.failed) {
    return <ReloadNotice subject="Deposit refunds" />;
  }

  if (!list?.enabled || list.refunds.length === 0) {
    return null;
  }

  return (
    <section className={styles.section} aria-label="Deposit refunds">
      <h3 className={styles.title}>Deposit refunds</h3>
      {list.refunds.map((refund) => (
        <article key={refund.paymentRequestId} className={styles.card}>
          <header className={styles.header}>
            <strong>{formatCurrency(refund.amountSen / 100)}</strong>
            <span>{refund.vehicle}</span>
          </header>
          <p className={styles.text}>{reasonText(refund)}</p>
          {refund.state === "processing" && (
            <p className={styles.status} role="status">
              Refund on its way to your account ending {refund.accountLast4}. It usually arrives
              within a business day.
            </p>
          )}
          {refund.state === "completed" && (
            <p className={styles.status} role="status">
              Refunded to your account ending {refund.accountLast4}.
            </p>
          )}
          {refund.state === "failed" && (
            <p className={styles.problem} role="alert">
              The refund could not be completed. Check your bank details and try again.
            </p>
          )}
          {(refund.state === "due" || refund.state === "failed") && (
            <RefundForm refund={refund} banks={list.banks} onSettled={refunds.reload} />
          )}
        </article>
      ))}
    </section>
  );
}

import Link from "next/link";
import type { Ref } from "react";
import { Button, buttonClassName } from "@/components/ui/Button";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { CustomerLookupRow } from "@/lib/staff/assisted-customer";
import styles from "./Sell.module.css";

export function SellerReceipt({
  receipt,
  customer,
  embedded,
  headingRef,
  onDone,
}: {
  receipt: { id: string; registrationPlate: string; slot: { startsAt: string } };
  customer: CustomerLookupRow | null;
  embedded: boolean;
  headingRef: Ref<HTMLHeadingElement>;
  onDone?: () => void;
}) {
  return (
    <section className={styles.unavailable}>
      <h1 ref={headingRef} tabIndex={-1}>
        {customer ? "Appointment confirmed" : "Your appointment is confirmed"}
      </h1>
      <p className={styles.plate}>{receipt.registrationPlate}</p>
      <p>{formatMalaysianDateTime(receipt.slot.startsAt)}</p>
      <p className={styles.help}>Reference: {receipt.id}</p>
      <p>
        {customer
          ? `Booked for ${customer.name}. The confirmation email is delivered to the customer separately, and the booking remains confirmed if mail is delayed.`
          : "A confirmation email is delivered separately. Your booking remains confirmed if mail is delayed."}
      </p>
      {embedded ? (
        <Button onClick={onDone}>Done</Button>
      ) : (
        <Link
          href={customer ? "/dashboard/intake" : "/account/appointments"}
          prefetch={false}
          className={buttonClassName("primary", "md")}
        >
          {customer ? "Open intake workspace" : "View your appointments"}
        </Link>
      )}
    </section>
  );
}

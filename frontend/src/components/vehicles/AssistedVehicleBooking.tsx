"use client";

import { useState, type ReactNode } from "react";
import { CustomerPicker } from "@/components/staff/CustomerPicker";
import type { CustomerAccount } from "@/lib/auth/contracts";
import { assistedAccount, type CustomerLookupRow } from "@/lib/staff/assisted-customer";
import styles from "./VehicleReservation.module.css";

// Sales staff choose the customer first, then reserve or book a test drive through the same controls a customer
// sees. The account carries the customer's masked contact, which is enough to name them in the confirmation.
export function AssistedVehicleBooking({
  financing,
  renderBooking,
}: {
  financing: ReactNode;
  renderBooking: (account: CustomerAccount) => ReactNode;
}) {
  const [customer, setCustomer] = useState<CustomerLookupRow | null>(null);

  return (
    <div className={styles.reservation}>
      <CustomerPicker
        // The reservation is this page's main action; a test drive limit still shows when it is tried.
        purpose="reservation"
        selected={customer}
        onSelect={setCustomer}
        onClear={() => setCustomer(null)}
      />
      {customer ? (
        renderBooking(assistedAccount(customer))
      ) : (
        <div className={styles.actions}>{financing}</div>
      )}
    </div>
  );
}

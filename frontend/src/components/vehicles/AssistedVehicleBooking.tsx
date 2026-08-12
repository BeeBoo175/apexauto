"use client";

import { useState, type ReactNode } from "react";
import { CustomerPicker } from "@/components/staff/CustomerPicker";
import type { CustomerAccount } from "@/lib/auth/contracts";
import type { CustomerLookupRow } from "@/lib/staff/assisted-customer";
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
        selected={customer}
        onSelect={setCustomer}
        onClear={() => setCustomer(null)}
      />
      {customer ? (
        renderBooking({
          userId: customer.id,
          legalName: customer.name,
          email: customer.email,
          emailVerified: true,
        })
      ) : (
        <div className={styles.actions}>{financing}</div>
      )}
    </div>
  );
}

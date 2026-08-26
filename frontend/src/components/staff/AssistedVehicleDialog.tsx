"use client";

import { useState } from "react";
import { CustomerReservation } from "@/components/vehicles/VehicleReservation";
import { VehicleTestDrive } from "@/components/vehicles/VehicleTestDrive";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useVehiclePicker } from "@/hooks/use-vehicle-picker";
import type { CurrentActor } from "@/lib/auth/actor";
import { assistedAccount, type CustomerLookupRow } from "@/lib/staff/assisted-customer";
import { CustomerPicker } from "./CustomerPicker";
import { VehiclePicker } from "./VehiclePicker";
import styles from "./AssistedDialogs.module.css";

export type AssistedVehicleKind = "testDrive" | "reservation";

const titles: Record<AssistedVehicleKind, string> = {
  testDrive: "Book a test drive for a customer",
  reservation: "Reserve a car for a customer",
};

const descriptions: Record<AssistedVehicleKind, string> = {
  testDrive: "Choose an existing customer, then the car. The customer is emailed the confirmation.",
  reservation:
    "Choose an existing customer, then the car. The customer is emailed a link to confirm the deposit.",
};

// Sales staff choose the customer and then the car, and the booking itself is the customer own control: the same
// slots, rules and confirmation, made for the customer, who is emailed the result.
export function AssistedVehicleDialog({
  actor,
  kind,
  open,
  onClose,
}: {
  actor: CurrentActor;
  kind: AssistedVehicleKind;
  open: boolean;
  onClose: () => void;
}) {
  const [customer, setCustomer] = useState<CustomerLookupRow | null>(null);
  const picker = useVehiclePicker();
  const detail = picker.detail;
  const listing = detail?.listing;
  const account = customer && assistedAccount(customer);

  return (
    <Dialog open={open} onClose={onClose} title={titles[kind]} description={descriptions[kind]}>
      <div className={styles.stack}>
        <CustomerPicker
          purpose={kind}
          selected={customer}
          disabled={Boolean(detail)}
          onSelect={setCustomer}
          onClear={() => setCustomer(null)}
        />
        {customer && !detail && <VehiclePicker picker={picker} />}
        {customer && account && detail && listing && (
          <>
            <p className={styles.chosen}>
              <span className={styles.chosenText}>
                <strong className={styles.vehicleName}>
                  {listing.manufactureYear} {listing.make} {listing.model} {listing.trim}
                </strong>
                <span className={styles.vehicleMeta}>{listing.branchName}</span>
                <a
                  className={styles.pageLink}
                  href={`/vehicles/${listing.id}`}
                  target="_blank"
                  rel="noopener"
                >
                  View car page
                </a>
              </span>
              <Button
                variant="secondary"
                size="compact"
                className={styles.change}
                onClick={picker.clear}
              >
                Change car
              </Button>
            </p>
            <div className={styles.actions}>
              {kind === "testDrive" ? (
                <VehicleTestDrive
                  detail={detail}
                  access={{ status: 200, actor, account }}
                  actingFor={customer.id}
                />
              ) : (
                <CustomerReservation
                  key={`${customer.id}:${detail.publicationId}`}
                  detail={detail}
                  account={account}
                  actingFor={customer.id}
                  promo={null}
                  disabled={false}
                  financing={null}
                />
              )}
            </div>
          </>
        )}
      </div>
    </Dialog>
  );
}

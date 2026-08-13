import { CustomerPicker } from "@/components/staff/CustomerPicker";
import type { CustomerLookupRow } from "@/lib/staff/assisted-customer";
import type { StaffSellNotice } from "@/lib/intake/staff-sell-notice";
import styles from "./Sell.module.css";

// Sales staff book the trade-in for an existing customer: they choose the account first, and the photos and
// appointment then belong to that customer. The customer cannot change once photos are chosen.
export function AssistedSellerGate({
  notice,
  customer,
  locked,
  showLink = true,
  onSelect,
  onClear,
}: {
  notice: StaffSellNotice;
  // Inside the staff workspace itself there is no workspace to link to.
  showLink?: boolean;
  customer: CustomerLookupRow | null;
  locked: boolean;
  onSelect: (customer: CustomerLookupRow) => void;
  onClear: () => void;
}) {
  return (
    <div className={styles.authGateCard}>
      {!customer && <p>{notice.message}</p>}
      <CustomerPicker selected={customer} disabled={locked} onSelect={onSelect} onClear={onClear} />
      {showLink && <a href={notice.href}>{notice.label}</a>}
    </div>
  );
}

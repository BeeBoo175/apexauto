import type { SaleContext } from "@/lib/sales/contracts";
import { StaffContact } from "@/components/staff/StaffContact";
import record from "@/components/staff/StaffRecordContent.module.css";
import styles from "./Sales.module.css";

export function SaleFacts({ context }: { context: SaleContext }) {
  return (
    <div className={styles.customer}>
      <section className={styles.customerSection} aria-label="Customer">
        <div className={record.group}>
          <h3 className={record.title}>{context.customer}</h3>
          <p>Copy the contact details to arrange a call or email.</p>
        </div>
        <dl className={styles.contactFacts}>
          {context.customerMobile && <StaffContact method="phone" value={context.customerMobile} />}
          {context.customerEmail && <StaffContact method="email" value={context.customerEmail} />}
        </dl>
      </section>
    </div>
  );
}

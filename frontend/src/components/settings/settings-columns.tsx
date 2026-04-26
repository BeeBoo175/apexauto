import { Badge } from "@/components/ui/Badge";
import type { DataColumn } from "@/components/ui/data-table/types";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import type { Promo, Warranty } from "@/lib/settings/settings";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

export function promoState(promo: Promo, now = Date.now()) {
  if (Date.parse(promo.expiresAtUtc) <= now) {
    return "Expired";
  }
  return promo.isActive ? "Active" : "Inactive";
}

function StateBadge({ state }: { state: string }) {
  const tone = state === "Active" ? tones.success : tones.neutral;
  return <Badge className={`${tone} ${tones.badge} ${styles.status}`}>{state}</Badge>;
}

export const promoColumns: DataColumn<Promo>[] = [
  {
    id: "code",
    label: "Code",
    sortable: true,
    ...staffColumnSizing.registration,
    cell: (row) => <strong>{row.code}</strong>,
  },
  {
    id: "discount",
    label: "Discount",
    sortable: true,
    ...staffColumnSizing.money,
    align: "right",
    cell: (row) => <span className={styles.money}>{formatCurrency(row.discount)}</span>,
  },
  {
    id: "expiresAt",
    label: "Expires",
    sortable: true,
    ...staffColumnSizing.dateTime,
    cell: (row) => (
      <time className={styles.date} dateTime={row.expiresAtUtc}>
        {formatMalaysianDateTime(row.expiresAtUtc)}
      </time>
    ),
  },
  {
    id: "status",
    label: "Status",
    sortable: true,
    ...staffColumnSizing.shortStatus,
    cell: (row) => <StateBadge state={promoState(row)} />,
  },
];

export const warrantyColumns: DataColumn<Warranty>[] = [
  {
    id: "name",
    label: "Package",
    sortable: true,
    ...staffColumnSizing.activity,
    cell: (row) => <strong>{row.name}</strong>,
  },
  {
    id: "term",
    label: "Term",
    sortable: true,
    width: 120,
    cell: (row) => `${row.durationMonths} months`,
  },
  {
    id: "mileage",
    label: "Mileage cap",
    sortable: true,
    width: 152,
    cell: (row) => `${row.mileageCapKm.toLocaleString("en-MY")} km`,
  },
  {
    id: "price",
    label: "Price",
    sortable: true,
    ...staffColumnSizing.money,
    align: "right",
    cell: (row) => <span className={styles.money}>{formatCurrency(row.price)}</span>,
  },
  {
    id: "grade",
    label: "Minimum grade",
    sortable: true,
    width: 152,
    cell: (row) => `Grade ${row.minimumGrade}`,
  },
  {
    id: "status",
    label: "Status",
    sortable: true,
    ...staffColumnSizing.shortStatus,
    cell: (row) => <StateBadge state={row.isActive ? "Active" : "Inactive"} />,
  },
];

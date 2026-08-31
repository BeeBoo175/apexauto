import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Badge } from "@/components/ui/Badge";
import type { DataColumn } from "@/components/ui/data-table/types";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import {
  listingActivity,
  listingInspectionLabel,
  listingVisibilityNote,
} from "@/lib/vehicles/listing-table";
import { listingPublicationState, type ListingItem } from "@/lib/vehicles/listings";
import tones from "@/components/ui/StatusTone.module.css";
import styles from "@/components/staff/StaffTable.module.css";

function PublicationCell({ item }: { item: ListingItem }) {
  const { label, tone } = listingPublicationState(item);
  const note = listingVisibilityNote(item);
  if (!item.revisionId && !item.publicationId) {
    return <span className={styles.muted}>{label}</span>;
  }
  return (
    <div className={styles.inline}>
      <Badge className={`${tones.badge} ${tones[tone]} ${styles.status}`}>{label}</Badge>
      {note && (
        <span className={`${styles.muted} ${styles.context}`} title={note}>
          {note}
        </span>
      )}
    </div>
  );
}

function PublishedPrice({ item }: { item: ListingItem }) {
  if (item.approvedVehiclePrice === null) {
    return <span className={styles.muted}>Not published</span>;
  }
  return (
    <div className={`${styles.inline} ${styles.amount}`}>
      <span className={styles.money}>{formatCurrency(item.approvedVehiclePrice)}</span>
      {!item.isPublic && (
        <span className={styles.muted} title="Last approved price; this listing is not live">
          Not live
        </span>
      )}
    </div>
  );
}

function DraftPrice({ item }: { item: ListingItem }) {
  if (item.vehiclePrice === null) {
    return <span className={styles.muted}>Not set</span>;
  }
  if (item.vehiclePrice === item.approvedVehiclePrice) {
    return (
      <span
        className={styles.muted}
        title="No saved price change"
        aria-label="No saved price change"
      >
        No change
      </span>
    );
  }
  return <span className={styles.money}>{formatCurrency(item.vehiclePrice)}</span>;
}

function InspectionCell({ item }: { item: ListingItem }) {
  const statusTones = {
    Available: "success",
    PendingInspection: "warning",
    Rejected: "danger",
    Sold: "neutral",
    Delivered: "neutral",
  } as const;
  return (
    <Badge className={`${tones.badge} ${tones[statusTones[item.vehicleStatus]]} ${styles.status}`}>
      {listingInspectionLabel(item)}
    </Badge>
  );
}

function ActivityCell({ item }: { item: ListingItem }) {
  const activity = listingActivity(item);
  if (!activity) {
    return <span className={styles.muted}>No listing activity</span>;
  }
  let tone = "neutral";
  if (activity.label === "Draft saved") {
    tone = "info";
  }
  if (activity.label === "Approved") {
    tone = "success";
  }
  return (
    <div className={styles.inline}>
      <Badge className={`${tones.badge} ${tones[tone]} ${styles.event}`}>{activity.label}</Badge>
      <time
        className={styles.date}
        dateTime={activity.at}
        title={formatMalaysianDateTime(activity.at)}
      >
        {formatMalaysianDateTime(activity.at)}
      </time>
    </div>
  );
}

export function listingColumns(showBranch: boolean): DataColumn<ListingItem>[] {
  const columns: DataColumn<ListingItem>[] = [
    {
      id: "registration",
      label: "Registration",
      sortable: true,
      ...staffColumnSizing.registration,
      cell: (row) => <VehicleRegistration registration={row.registrationPlate} />,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      sortable: true,
      ...staffColumnSizing.vehicle,
      cell: (row) => <VehicleDescription vehicle={row} />,
    },
    {
      id: "publication",
      label: "Publication",
      sortable: true,
      ...staffColumnSizing.statusWithContext,
      cell: (row) => <PublicationCell item={row} />,
    },
    {
      id: "inspection",
      label: "Inspection",
      sortable: true,
      ...staffColumnSizing.shortStatus,
      cell: (row) => <InspectionCell item={row} />,
    },
    {
      id: "publishedPrice",
      label: "Published price",
      sortable: true,
      ...staffColumnSizing.money,
      align: "right",
      cell: (row) => <PublishedPrice item={row} />,
    },
    {
      id: "price",
      label: "Draft price",
      sortable: true,
      ...staffColumnSizing.money,
      align: "right",
      cell: (row) => <DraftPrice item={row} />,
    },
    {
      id: "activity",
      label: "Listing activity",
      sortable: true,
      ...staffColumnSizing.activity,
      cell: (row) => <ActivityCell item={row} />,
    },
  ];
  if (showBranch) {
    columns.push({
      id: "branch",
      label: "Branch",
      sortable: true,
      ...staffColumnSizing.branch,
      cell: (row) => row.branchName,
    });
  }
  return columns;
}

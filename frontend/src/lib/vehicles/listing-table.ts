import type { ListingItem } from "./listings";

export function listingInspectionLabel(item: ListingItem) {
  if (item.vehicleStatus === "Sold" || item.vehicleStatus === "Delivered") {
    return item.vehicleStatus;
  }
  if (item.vehicleStatus === "Available") {
    return "Cleared";
  }
  return item.vehicleStatus === "Rejected" ? "Rejected" : "Not cleared";
}

export function listingVisibilityNote(item: ListingItem) {
  if (item.isPublic) {
    return item.revisionId !== item.publishedRevisionId ? "Saved changes not live" : "";
  }
  if (item.isWithdrawn || !item.publicationId) {
    return "";
  }
  const reasons: string[] = [];
  if (item.isReserved) {
    reasons.push("Reserved");
  }
  if (item.vehicleStatus !== "Available") {
    reasons.push(listingInspectionLabel(item));
  }
  if (item.vehicleStatus === "Available" && !item.hasCurrentPublicationInspection) {
    reasons.push("Inspection approval changed");
  }
  if (item.revisionId !== item.publishedRevisionId) {
    reasons.push("Saved changes not approved");
  }
  return reasons.join(" · ");
}

export function listingActivity(item: ListingItem) {
  const events = [
    { label: "Draft saved", at: item.savedAt },
    { label: "Approved", at: item.approvedAt },
    { label: "Withdrawn", at: item.withdrawnAt },
  ];
  return events.reduce<{ label: string; at: string } | null>((latest, event) => {
    if (!event.at) {
      return latest;
    }
    if (!latest || Date.parse(event.at) >= Date.parse(latest.at)) {
      return { label: event.label, at: event.at };
    }
    return latest;
  }, null);
}

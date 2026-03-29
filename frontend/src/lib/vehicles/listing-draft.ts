import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseMyr } from "@/lib/currency-input";
import { fuels, transmissions } from "./contracts";
import { parseRevision, type ListingRevision } from "./listing-workspace";

export interface ListingDraft {
  vehiclePrice: string;
  roadTax: string;
  transmission: string;
  fuel: string;
  description: string;
  photoIds: string[];
}

export interface ListingSaveReceipt extends ListingRevision {
  actorId: string;
}

export function listingDraft(revision: ListingRevision | null): ListingDraft {
  return {
    vehiclePrice: revision?.vehiclePrice.toFixed(2) ?? "",
    roadTax: revision?.roadTax.toFixed(2) ?? "",
    transmission: revision?.transmission ?? "",
    fuel: revision?.fuel ?? "",
    description: revision?.description ?? "",
    photoIds: [...(revision?.photoIds ?? [])],
  };
}

export function listingDraftContents(value: ListingDraft) {
  const vehiclePrice = parseMyr(value.vehiclePrice);
  const roadTax = parseMyr(value.roadTax);
  const transmission = transmissions.find((name) => name === value.transmission);
  const fuel = fuels.find((name) => name === value.fuel);
  const description = value.description.trim();
  if (
    vehiclePrice === null ||
    roadTax === null ||
    !transmission ||
    !fuel ||
    !description ||
    description.length > 2000 ||
    description.includes("\0") ||
    !Array.isArray(value.photoIds) ||
    !value.photoIds.every(isUuid) ||
    new Set(value.photoIds).size !== value.photoIds.length
  ) {
    return null;
  }
  return {
    vehiclePrice,
    roadTax,
    transmission: transmissions.indexOf(transmission),
    fuel: fuels.indexOf(fuel),
    description,
    photoIds: [...value.photoIds],
  };
}

export function parseListingSave(value: unknown): ListingSaveReceipt | null {
  if (!isRecord(value) || (value.status !== 0 && value.status !== "Succeeded")) {
    return null;
  }
  return parseListingReceipt(value.draft);
}

export function parseListingReceipt(value: unknown): ListingSaveReceipt | null {
  const draft = parseRevision(value);
  if (!draft || !isRecord(value) || !isUuid(value.actorId)) {
    return null;
  }
  return { ...draft, actorId: value.actorId };
}

export function matchesListingSave(receipt: ListingSaveReceipt, body: unknown) {
  if (!isRecord(body) || !Number.isSafeInteger(body.expectedVersion)) {
    return false;
  }
  return (
    receipt.id === body.operationId &&
    receipt.version === Number(body.expectedVersion) + 1 &&
    receipt.vehiclePrice === body.vehiclePrice &&
    receipt.roadTax === body.roadTax &&
    transmissions.indexOf(receipt.transmission) === body.transmission &&
    fuels.indexOf(receipt.fuel) === body.fuel &&
    receipt.description === body.description &&
    JSON.stringify(receipt.photoIds) === JSON.stringify(body.photoIds)
  );
}

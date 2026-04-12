import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { parseReservation, parseReservationRow } from "./parse-reservations";
import type { ReservationItem } from "./reservations";

export interface ReservationDetail {
  reservation: ReservationItem;
  publicationId: string;
  price: {
    vehiclePrice: number;
    processingFee: number;
    roadTax: number;
    serviceTax: number;
    promoDiscount: number;
    total: number;
  };
  deposit: number;
  promoCode: string | null;
  release: { id: string; reason: string | null; recordedAt: string; refund: number } | null;
}

function terms(value: unknown, reservation: ReservationItem | null): ReservationDetail | null {
  if (!isRecord(value) || !reservation || !isRecord(value.price)) {
    return null;
  }
  const price = value.price;
  if (
    !isUuid(value.publicationId) ||
    !isMyr(value.deposit) ||
    value.deposit !== 500 ||
    !isMyr(price.vehiclePrice) ||
    !isMyr(price.processingFee) ||
    !isMyr(price.roadTax) ||
    !isMyr(price.serviceTax) ||
    !isMyr(price.promoDiscount) ||
    !isMyr(price.total) ||
    price.total !== reservation.total ||
    price.promoDiscount > price.vehiclePrice ||
    Math.round(price.total * 100) !==
      Math.round(price.vehiclePrice * 100) +
        Math.round(price.processingFee * 100) +
        Math.round(price.roadTax * 100) +
        Math.round(price.serviceTax * 100) -
        Math.round(price.promoDiscount * 100) ||
    (value.promoCode !== null && (typeof value.promoCode !== "string" || !value.promoCode.trim()))
  ) {
    return null;
  }
  let release: ReservationDetail["release"] = null;
  if (reservation.status !== "Active") {
    const refunded = value.release;
    if (
      !isRecord(refunded) ||
      !isUuid(refunded.id) ||
      typeof refunded.recordedAt !== "string" ||
      !/(?:Z|\+00:00)$/.test(refunded.recordedAt) ||
      !Number.isFinite(Date.parse(refunded.recordedAt)) ||
      Date.parse(refunded.recordedAt) < Date.parse(reservation.createdAt) ||
      refunded.refund !== value.deposit ||
      (refunded.reason !== null &&
        (typeof refunded.reason !== "string" || !refunded.reason.trim())) ||
      (reservation.status === "StaffCancelled" && !refunded.reason)
    ) {
      return null;
    }
    release = {
      id: refunded.id,
      reason: refunded.reason,
      recordedAt: refunded.recordedAt,
      refund: refunded.refund,
    };
  } else if (value.release !== null) {
    return null;
  }
  return {
    reservation,
    publicationId: value.publicationId,
    price: {
      vehiclePrice: price.vehiclePrice,
      processingFee: price.processingFee,
      roadTax: price.roadTax,
      serviceTax: price.serviceTax,
      promoDiscount: price.promoDiscount,
      total: price.total,
    },
    deposit: value.deposit,
    promoCode: value.promoCode,
    release,
  };
}

export function parseReservationDetail(value: unknown): ReservationDetail | null {
  if (
    !isRecord(value) ||
    (value.status !== 0 && value.status !== "Succeeded") ||
    !isRecord(value.item) ||
    !isRecord(value.item.receipt)
  ) {
    return null;
  }
  return terms(
    { ...value.item.receipt, release: value.item.release },
    parseReservation(value.item),
  );
}

export function parseReservationDetailSnapshot(value: unknown): ReservationDetail | null {
  if (!isRecord(value)) {
    return null;
  }
  return terms(value, parseReservationRow(value.reservation));
}

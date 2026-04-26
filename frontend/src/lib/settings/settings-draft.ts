import { parseMyr } from "@/lib/currency-input";
import type { PurchaseSettings, SettingsBounds } from "./settings";

export interface SettingsDraft {
  processingFee: string;
  sstPercent: string;
  maxAgentDiscountAmount: string;
  reservationHoldHours: string;
}

export type SettingsDraftErrors = Partial<Record<keyof SettingsDraft, string>>;

export interface SettingsValues {
  processingFee: number;
  sstRate: number;
  maxAgentDiscountAmount: number;
  reservationHoldHours: number;
}

const money = (value: number) => value.toFixed(2);
const malaysiaOffsetMs = 8 * 60 * 60 * 1000;

// Expiry is entered as Malaysia local time (UTC+8, no daylight saving) and stored as UTC.
export function malaysiaInput(utc: string) {
  return new Date(Date.parse(utc) + malaysiaOffsetMs).toISOString().slice(0, 16);
}

export function utcFromMalaysiaInput(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) {
    return null;
  }
  const time = Date.parse(`${value}:00+08:00`);
  return Number.isFinite(time) ? new Date(time).toISOString() : null;
}

export function draftFrom(settings: PurchaseSettings): SettingsDraft {
  return {
    processingFee: money(settings.processingFee),
    // The rate is stored as a fraction and entered as a percentage.
    sstPercent: String(Number((settings.sstRate * 100).toFixed(2))),
    maxAgentDiscountAmount: money(settings.maxAgentDiscountAmount),
    reservationHoldHours: String(settings.reservationHoldHours),
  };
}

export function readDraft(draft: SettingsDraft, bounds: SettingsBounds) {
  const errors: SettingsDraftErrors = {};
  const processingFee = parseMyr(draft.processingFee.trim());
  if (processingFee === null || processingFee > bounds.maximumFee) {
    errors.processingFee = `Enter an amount from RM 0.00 to RM ${money(bounds.maximumFee)}.`;
  }
  const maximumPercent = Number((bounds.maximumSstRate * 100).toFixed(2));
  const percent = /^\d+(?:\.\d{1,2})?$/.test(draft.sstPercent.trim())
    ? Number(draft.sstPercent.trim())
    : null;
  if (percent === null || percent > maximumPercent) {
    errors.sstPercent = `Enter a rate from 0% to ${maximumPercent}% with up to two decimals.`;
  }
  const discount = parseMyr(draft.maxAgentDiscountAmount.trim());
  if (discount === null || discount > bounds.maximumDiscount) {
    errors.maxAgentDiscountAmount = `Enter an amount from RM 0.00 to RM ${money(bounds.maximumDiscount)}.`;
  }
  const hours = /^\d+$/.test(draft.reservationHoldHours.trim())
    ? Number(draft.reservationHoldHours.trim())
    : null;
  if (hours === null || hours < bounds.minimumHoldHours || hours > bounds.maximumHoldHours) {
    errors.reservationHoldHours = `Enter whole hours from ${bounds.minimumHoldHours} to ${bounds.maximumHoldHours}.`;
  }
  if (Object.keys(errors).length > 0) {
    return { values: null, errors };
  }
  const values: SettingsValues = {
    processingFee: processingFee!,
    // Rounding to four decimals removes binary noise such as 7.35 / 100.
    sstRate: Number((percent! / 100).toFixed(4)),
    maxAgentDiscountAmount: discount!,
    reservationHoldHours: hours!,
  };
  return { values, errors };
}

export function unchanged(values: SettingsValues, settings: PurchaseSettings) {
  return (
    values.processingFee === settings.processingFee &&
    values.sstRate === settings.sstRate &&
    values.maxAgentDiscountAmount === settings.maxAgentDiscountAmount &&
    values.reservationHoldHours === settings.reservationHoldHours
  );
}

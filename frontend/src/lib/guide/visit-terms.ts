// Documented defaults that mirror the deployed InspectionSchedule configuration; customer copy reads them from here.
export const SHOWROOM_OPENING_MINUTES = 9 * 60;
export const SHOWROOM_CLOSING_MINUTES = 18 * 60;
export const INSPECTION_BAY_MINUTES = 90;
export const BOOKING_NOTICE_HOURS = 2;
export const BOOKING_NOTICE_LABEL = "two hours";
export const BOOKING_HORIZON_DAYS = 30;

// calendar.ts hourLabel renders "9am", so the 24-hour clock label lives here.
export function clockLabel(minutes: number) {
  const hours = String(Math.floor(minutes / 60)).padStart(2, "0");
  const remainder = String(minutes % 60).padStart(2, "0");
  return `${hours}:${remainder}`;
}

export const SHOWROOM_OPENS_AT = clockLabel(SHOWROOM_OPENING_MINUTES);
export const SHOWROOM_CLOSES_AT = clockLabel(SHOWROOM_CLOSING_MINUTES);

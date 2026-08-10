import { addCalendarDays, calendarDate, isCalendarDate } from "@/lib/calendar";

export const INTAKE_HORIZON_DAYS = 30;

export interface IntakeDateWindow {
  firstDate: string;
  lastDate: string;
}

export function defaultIntakeDateWindow(now = new Date()): IntakeDateWindow {
  const firstDate = calendarDate(now);
  return {
    firstDate,
    lastDate: addCalendarDays(firstDate, INTAKE_HORIZON_DAYS - 1),
  };
}

export function isSundayDate(date: string): boolean {
  if (!isCalendarDate(date)) {
    return false;
  }
  return new Date(`${date}T00:00:00Z`).getUTCDay() === 0;
}

export function validateIntakeDate(value: string, window: IntakeDateWindow): string | null {
  if (!value) {
    return null;
  }
  if (!isCalendarDate(value)) {
    return "Enter a valid calendar date.";
  }
  if (value < window.firstDate || value > window.lastDate) {
    return "Choose a date from today up to 30 days ahead (Malaysia time).";
  }
  if (isSundayDate(value)) {
    return "Showrooms are closed on Sundays. Choose Monday to Saturday.";
  }
  return null;
}

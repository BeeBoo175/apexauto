export function isCalendarDate(value: unknown): value is string {
  if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }
  const stamp = Date.parse(`${value}T00:00:00Z`);
  return (
    Number.isFinite(stamp) &&
    new Date(stamp).toISOString().slice(0, 10) === value &&
    value >= "0002-01-01" &&
    value <= "9998-12-31"
  );
}

export function calendarDate(now = new Date(), timeZone = "Asia/Kuala_Lumpur") {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(now);
  const part = (type: string) => parts.find((value) => value.type === type)!.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
}

export function addCalendarDays(date: string, days: number) {
  return new Date(Date.parse(`${date}T00:00:00Z`) + days * 86400000).toISOString().slice(0, 10);
}

export function calendarRange(date: string) {
  const first = `${date.slice(0, 7)}-01`;
  const weekday = new Date(`${first}T00:00:00Z`).getUTCDay();
  const from = addCalendarDays(first, -((weekday + 6) % 7));
  return { from, to: addCalendarDays(from, 41) };
}

export function calendarWeeks(date: string) {
  const { from } = calendarRange(date);
  return Array.from({ length: 6 }, (_, week) =>
    Array.from({ length: 7 }, (_, day) => addCalendarDays(from, week * 7 + day)),
  );
}

export function addCalendarMonths(date: string, months: number) {
  const next = new Date(`${date.slice(0, 7)}-01T00:00:00Z`);
  next.setUTCMonth(next.getUTCMonth() + months);
  return next.toISOString().slice(0, 10);
}

export function calendarDayLabel(date: string, locale = "en-MY") {
  return new Intl.DateTimeFormat(locale, {
    timeZone: "UTC",
    weekday: "long",
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(new Date(`${date}T00:00:00Z`));
}

export function calendarTime(stamp: string, timeZone: string, locale = "en-MY", hour12 = true) {
  return new Intl.DateTimeFormat(locale, {
    timeZone,
    hour: "2-digit",
    minute: "2-digit",
    hour12,
  }).format(new Date(stamp));
}

export function hourLabel(hour: number) {
  const suffix = hour < 12 ? "am" : "pm";
  const display = hour % 12 === 0 ? 12 : hour % 12;
  return `${display}${suffix}`;
}

const malaysiaOffsetMs = 8 * 60 * 60 * 1000;

// Malaysia local time is UTC+8 without daylight saving; stored timestamps remain UTC.
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

export function formatMalaysianDateTime(
  utcDate: string | Date,
  options?: Intl.DateTimeFormatOptions,
): string {
  const date = typeof utcDate === "string" ? new Date(utcDate) : utcDate;

  const defaultOptions: Intl.DateTimeFormatOptions = {
    timeZone: "Asia/Kuala_Lumpur",
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
    ...options,
  };

  // ICU separates the time and am/pm with a narrow no-break space, which renders as stray characters in some fonts.
  return new Intl.DateTimeFormat("en-MY", defaultOptions)
    .format(date)
    .replace(/[\u202f\u00a0]/g, " ");
}

export function formatMalaysianDate(utcDate: string | Date): string {
  return formatMalaysianDateTime(utcDate, {
    hour: undefined,
    minute: undefined,
    hour12: undefined,
  });
}

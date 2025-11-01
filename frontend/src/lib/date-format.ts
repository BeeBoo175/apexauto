export function formatMalaysianDateTime(
  utcDate: string | Date,
  options?: Intl.DateTimeFormatOptions
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

  return new Intl.DateTimeFormat("en-MY", defaultOptions).format(date);
}

export function formatMalaysianDate(utcDate: string | Date): string {
  return formatMalaysianDateTime(utcDate, {
    hour: undefined,
    minute: undefined,
    hour12: undefined,
  });
}

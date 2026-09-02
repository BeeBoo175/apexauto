import { parseMyr } from "./currency-input";

export function validateMoneyRange(lower: string, upper: string) {
  const low = lower ? parseMyr(lower) : null;
  const high = upper ? parseMyr(upper) : null;
  const invalidMinimum = Boolean(lower) && low === null;
  const invalidMaximum = Boolean(upper) && high === null;
  const invalidOrder = low !== null && high !== null && low > high;
  if (invalidMinimum || invalidMaximum || invalidOrder) {
    return { error: "Enter valid RM amounts with the minimum no greater than the maximum." };
  }
  return { lower, upper };
}

export function validateHoursRange(lower: string, upper: string, maximumHours: number) {
  const first = lower.trim();
  const last = upper.trim();
  const invalidValue = [first, last]
    .filter(Boolean)
    .some(
      (value) =>
        !/^\d+(\.\d+)?$/.test(value) ||
        !Number.isFinite(Number(value)) ||
        Number(value) > maximumHours,
    );
  if (invalidValue || Boolean(first && last && Number(first) > Number(last))) {
    return {
      error: `Enter hours from 0 to ${maximumHours} with the minimum no greater than the maximum.`,
    };
  }
  return { lower: first, upper: last };
}

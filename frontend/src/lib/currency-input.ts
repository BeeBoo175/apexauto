export function parseMyr(value: string): number | null {
  if (!/^\d+(?:\.\d{1,2})?$/.test(value)) {
    return null;
  }
  const [whole, cents = ""] = value.split(".");
  const normalized = `${BigInt(whole)}.${cents.padEnd(2, "0")}`;
  const number = Number(normalized);
  if (
    !Number.isFinite(number) ||
    number * 100 > Number.MAX_SAFE_INTEGER ||
    number.toFixed(2) !== normalized
  ) {
    return null;
  }
  return number;
}

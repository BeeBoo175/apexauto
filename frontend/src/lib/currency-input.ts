export function parseMyr(value: string): number | null {
  const input = value.trim();
  if (!/^\d+(?:\.\d{1,2})?$/.test(input)) {
    return null;
  }
  const [whole, cents = ""] = input.split(".");
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

export function isMyr(value: unknown): value is number {
  return (
    typeof value === "number" && Number.isFinite(value) && parseMyr(value.toFixed(2)) === value
  );
}

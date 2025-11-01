export function formatCurrency(amount: number, currency = "MYR"): string {
  const formatted = new Intl.NumberFormat("en-MY", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);

  return `${currency} ${formatted}`;
}

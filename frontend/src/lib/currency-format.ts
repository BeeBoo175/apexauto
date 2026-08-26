export function formatCurrency(amount: number, currency = "MYR"): string {
  const formatted = new Intl.NumberFormat("en-MY", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);

  const label = currency === "MYR" ? "RM" : currency;
  return `${label} ${formatted}`;
}

const compactCurrency = new Intl.NumberFormat("en-MY", {
  style: "currency",
  currency: "MYR",
  notation: "compact",
  maximumFractionDigits: 1,
});

// Short RM amounts for chart scales, such as "RM 1.2M"; hovers and tables carry the exact amount.
export function compactMoney(amount: number) {
  return compactCurrency.format(amount);
}

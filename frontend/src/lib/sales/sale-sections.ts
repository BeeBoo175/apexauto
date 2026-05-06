export const saleSections = ["customer", "deal", "history"] as const;

export type SaleSection = (typeof saleSections)[number];

export const saleSectionLabels: Record<SaleSection, string> = {
  customer: "Customer & car",
  deal: "Deal",
  history: "History",
};

// An unknown or missing address value opens the Deal section, where the terms and actions are.
export function saleSection(value: unknown): SaleSection {
  return saleSections.find((section) => section === value) ?? "deal";
}

// An address value that is not a positive whole number opens no past version.
export function saleVersionNumber(value: unknown): number | null {
  const parsed = typeof value === "string" && /^[1-9]\d{0,9}$/.test(value) ? Number(value) : null;
  return parsed !== null && Number.isSafeInteger(parsed) ? parsed : null;
}

export const validSaleReason = (reason: string) =>
  reason === reason.trim() && reason.length >= 1 && reason.length <= 1000;

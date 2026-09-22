import { formatCurrency } from "@/lib/currency-format";

export const catalogueBudgetPresets = [
  {
    id: "under-60k",
    label: `Under ${formatCurrency(60000)}`,
    shortLabel: "< 60k",
    minimumPrice: "",
    maximumPrice: "60000",
  },
  {
    id: "60k-100k",
    label: `${formatCurrency(60000)} – ${formatCurrency(100000)}`,
    shortLabel: "60k–100k",
    minimumPrice: "60000",
    maximumPrice: "100000",
  },
  {
    id: "above-100k",
    label: `Above ${formatCurrency(100000)}`,
    shortLabel: "> 100k",
    minimumPrice: "100000",
    maximumPrice: "",
  },
] as const;

export const catalogueBudgetOptions = [
  { value: "", label: "Any budget" },
  ...catalogueBudgetPresets
    .filter((preset) => preset.maximumPrice)
    .map((preset) => ({
      value: preset.maximumPrice,
      label: `Up to ${formatCurrency(Number(preset.maximumPrice))}`,
    })),
];

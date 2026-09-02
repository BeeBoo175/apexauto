export type SaleMetric = "balance" | "discount" | "timeLeft" | "status";

export const saleMetrics: { value: SaleMetric; label: string; heading: string }[] = [
  { value: "balance", label: "Balance", heading: "Balance payable" },
  { value: "discount", label: "Discount", heading: "Discount" },
  { value: "timeLeft", label: "Time left", heading: "Time left" },
  { value: "status", label: "Status", heading: "Status" },
];

const alphabetical = { asc: "A to Z", desc: "Z to A" };
const amount = { asc: "Lowest first", desc: "Highest first" };

export const saleSortLabels = {
  reference: alphabetical,
  registration: alphabetical,
  vehicle: alphabetical,
  customer: alphabetical,
  agent: alphabetical,
  branch: alphabetical,
  balance: amount,
  discount: amount,
  timeLeft: { asc: "Expiring soonest", desc: "Most time left" },
  status: { asc: "Lifecycle order", desc: "Reverse lifecycle" },
};

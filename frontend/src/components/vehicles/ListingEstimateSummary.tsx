import { Button } from "@/components/ui/Button";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import type { ListingEstimate } from "@/lib/vehicles/listing-estimate";
import { VehiclePurchaseSummary } from "./VehiclePurchaseSummary";

export function ListingEstimateSummary({
  estimate,
  roadTax,
  incomplete,
  busy,
  failed,
  onRetry,
}: {
  estimate: ListingEstimate | null;
  roadTax: string;
  incomplete: boolean;
  busy: boolean;
  failed: boolean;
  onRetry: () => void;
}) {
  const price = estimate?.price;
  const localRoadTax = parseMyr(roadTax);
  const breakdown = estimate
    ? [
        { label: "Processing fee", amount: formatCurrency(estimate.price.processingFee) },
        { label: "Road tax", amount: formatCurrency(estimate.price.roadTax) },
        {
          label: `SST on processing fee (${estimate.sstRate * 100}%)`,
          amount: formatCurrency(estimate.price.serviceTax),
        },
      ]
    : [
        {
          label: "Road tax",
          amount: localRoadTax === null ? "Incomplete" : formatCurrency(localRoadTax),
        },
      ];
  let notice = "Draft estimate using current fees.";
  if (incomplete) {
    notice = "Enter a valid vehicle price and road tax to estimate the total.";
  } else if (failed) {
    notice = price ? "Refresh failed. Previous estimate shown." : "Draft estimate unavailable.";
  } else if (busy) {
    notice = "Updating draft estimate…";
  }

  return (
    <div aria-busy={busy}>
      <VehiclePurchaseSummary
        total={price ? formatCurrency(price.total) : null}
        breakdown={breakdown}
        notice={notice}
      />
      {failed && (
        <Button size="compact" variant="secondary" disabled={busy} onClick={onRetry}>
          Retry estimate
        </Button>
      )}
    </div>
  );
}

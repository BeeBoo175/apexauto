"use client";

import { StaffRecordLoadingShell } from "@/components/staff/StaffRecordLoadingShell";
import { StaffLedger } from "@/components/staff/StaffLedger";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useSaleVersion } from "@/hooks/use-sale-version";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { SaleContext } from "@/lib/sales/sale";
import { historyTitle } from "@/lib/sales/sale-history";
import type { SaleSection } from "@/lib/sales/sale-sections";
import { SaleFacts } from "./SaleFacts";
import { SaleLines } from "./SaleLines";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Sales.module.css";

const loadingDetails = ["Status", "Price", "Handling"];

// One past version of the sale, read only, in the section its action belongs to; changed fields are marked
// against the version before it. Back to current returns to the live section without touching any draft.
export function SalePastVersion({
  saleId,
  version,
  section,
  context,
  onBack,
}: {
  saleId: string;
  version: number;
  section: SaleSection;
  context: SaleContext;
  onBack: () => void;
}) {
  const review = useSaleVersion(saleId, version);
  const { result } = review;
  const title = result
    ? historyTitle({ kind: result.kind, toStatus: result.snapshot.status, agent: result.agent })
    : "";
  return (
    <section className={workbench.stack} aria-label="Past version" aria-busy={review.busy}>
      <LoadingProgress label="Loading the past version" active={review.busy} />
      <div className={styles.versionBar} role="status">
        <span>
          {result
            ? `Viewing version ${version} · ${title} by ${result.actor}, ${formatMalaysianDateTime(result.recordedAt)}`
            : `Viewing version ${version}`}
        </span>
        <Button size="compact" variant="secondary" onClick={onBack}>
          Back to current
        </Button>
      </div>
      {review.busy && (
        <StaffRecordLoadingShell label="Loading the sale as it stood…" details={loadingDetails} />
      )}
      {review.failed && (
        <div className={workbench.protocol} role="alert">
          <p>This version could not be loaded.</p>
          <Button size="compact" variant="secondary" onClick={review.retry}>
            Retry version
          </Button>
        </div>
      )}
      {/* Hold time and claim age are read as they stood when this version was recorded. */}
      {result && section === "customer" && (
        <SaleFacts
          sale={result.snapshot}
          context={context}
          agent={result.agent}
          decidedBy={null}
          now={Date.parse(result.recordedAt)}
          previous={result.previous}
        />
      )}
      {result && section !== "customer" && (
        <>
          <SaleLines sale={result.snapshot} previous={result.previous} />
          <StaffLedger title="Terms">
            <div>
              <dt>Purchase order</dt>
              <dd>{result.snapshot.orderNumber ?? "Not yet submitted"}</dd>
            </div>
            {result.snapshot.discountLimitAtSubmit !== null && (
              <div>
                <dt>Agent discount limit when submitted</dt>
                <dd>{formatCurrency(result.snapshot.discountLimitAtSubmit)}</dd>
              </div>
            )}
            {result.reason && (
              <div>
                <dt>Reason</dt>
                <dd>{result.reason}</dd>
              </div>
            )}
          </StaffLedger>
        </>
      )}
    </section>
  );
}

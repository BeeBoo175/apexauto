import { StaffDetailsDialog, type StaffDetail } from "@/components/staff/StaffDetailsDialog";
import { Badge } from "@/components/ui/Badge";
import { ResolvedPersonLabel } from "@/components/staff/ResolvedPersonLabel";
import { StaffRecordSummary, StaffSummaryFact } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { saleHasHoldTimer, type SaleSnapshot } from "@/lib/sales/contracts";
import { historyTitle, type SaleVersion } from "@/lib/sales/sale-history";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { SaleStatus } from "./SaleStatus";
import styles from "./Sales.module.css";
import tones from "@/components/ui/StatusTone.module.css";

type Editor = ReturnType<typeof useSaleEditor>;

function saleDetails(
  editor: Editor,
  sale: SaleSnapshot,
  retained: SaleVersion | null,
): StaffDetail[] {
  const { context } = editor.snapshot;
  const details: StaffDetail[] = [
    { label: "Reservation", value: context.reservationNumber },
    { label: "Branch", value: context.branch },
    { label: "Reserved", value: formatMalaysianDateTime(context.reservedAt) },
    { label: "Deposit credit", value: formatCurrency(sale.depositCredit) },
    { label: "Version", value: sale.version },
  ];
  if (sale.orderNumber) {
    details.push({ label: "Purchase order", value: sale.orderNumber });
  }
  if (retained) {
    details.push(
      { label: "Recorded", value: formatMalaysianDateTime(retained.recordedAt) },
      {
        label: "Action",
        value: historyTitle({ kind: retained.kind, toStatus: sale.status, agent: retained.agent }),
      },
      { label: "By", value: retained.actor },
    );
  } else {
    details.push({
      label: "Last activity",
      value: formatMalaysianDateTime(editor.snapshot.updatedAt),
    });
  }
  if (sale.claimedAt) {
    details.push({ label: "Claimed", value: formatMalaysianDateTime(sale.claimedAt) });
  }
  if (sale.decidedAt) {
    details.push({ label: "Approved", value: formatMalaysianDateTime(sale.decidedAt) });
  }
  if (sale.submittedAt) {
    details.push({ label: "Submitted", value: formatMalaysianDateTime(sale.submittedAt) });
  }
  if (!retained && editor.snapshot.decidedBy) {
    details.push({ label: "Approved by", value: editor.snapshot.decidedBy });
  }
  if (sale.closedAt) {
    details.push({ label: "Closed", value: formatMalaysianDateTime(sale.closedAt) });
  }
  return details;
}

// The selected record owns the summary as well as the tab content; historical timers use its recorded time.
export function SaleRecord({
  editor,
  now,
  retained,
  loadingVersion,
  versionFailed,
}: {
  editor: Editor;
  now: number;
  retained: SaleVersion | null;
  loadingVersion: boolean;
  versionFailed: boolean;
}) {
  const sale = retained?.snapshot ?? editor.snapshot.sale;
  const agent = retained ? retained.agent : editor.snapshot.agent;
  const at = retained ? Date.parse(retained.recordedAt) : now;
  const overLimit =
    sale.discountLimitAtSubmit !== null && sale.negotiatedDiscount > sale.discountLimitAtSubmit;
  const running = saleHasHoldTimer(sale.status);
  let balance = formatCurrency(sale.balancePayable);
  if (loadingVersion) {
    balance = versionFailed ? "Unavailable" : "Loading…";
  }

  return (
    <StaffRecordSummary
      ariaLabel="Sale balance and status"
      label="Balance payable"
      value={balance}
      busy={loadingVersion && !versionFailed}
      status={
        loadingVersion ? (
          <span className={styles.statusPlaceholder} aria-hidden="true" />
        ) : (
          <StaffStatusGroup
            label="Sale status"
            attention={
              overLimit && (
                <Badge className={`${tones.badge} ${tones.warning}`}>Discount over limit</Badge>
              )
            }
          >
            <SaleStatus status={sale.status} />
          </StaffStatusGroup>
        )
      }
      context={
        <>
          <div className={styles.summaryClock}>
            {loadingVersion && <span className={styles.clockPlaceholder} aria-hidden="true" />}
            {!loadingVersion && running && (
              <HoldTimer dueAt={sale.dueAt} pausedSeconds={sale.pausedSeconds} now={at} compact />
            )}
          </div>
          <StaffDetailsDialog
            title="Sale details"
            triggerLabel="View sale details"
            disabled={loadingVersion}
            details={saleDetails(editor, sale, retained)}
          />
        </>
      }
      facts={
        <StaffSummaryFact label="Handling agent">
          {!loadingVersion && agent && sale.agentId ? (
            <ResolvedPersonLabel id={sale.agentId} identity={agent} />
          ) : (
            <span aria-hidden={loadingVersion || undefined}>
              {loadingVersion ? "" : "Unassigned"}
            </span>
          )}
        </StaffSummaryFact>
      }
    >
      {editor.stale && (
        <p role="status">
          A newer version of this sale is available. Your terms remain retained; use the latest
          values before saving again.
        </p>
      )}
      {editor.confirmed && (
        <p role="alert">
          The action was confirmed, but the latest sale could not be read. Refresh before another
          action.
        </p>
      )}
      {editor.uncertain && (
        <p role="alert">
          The last action is unconfirmed. Repeat it to reconcile the same operation.
        </p>
      )}
    </StaffRecordSummary>
  );
}

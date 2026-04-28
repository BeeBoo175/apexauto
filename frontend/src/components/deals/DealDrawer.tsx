"use client";

import { useState } from "react";
import { SettingsDrawerLoadingShell } from "@/components/settings/SettingsDrawerLoadingShell";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Dialog } from "@/components/ui/Dialog";
import { Textarea } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useToast } from "@/components/ui/Toast";
import { useSettingsSave } from "@/hooks/use-settings-save";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { dealAbilities, dealEventLabels, validDealReason } from "@/lib/deals/deal-rules";
import { dealPath, dealStatusLabels, parseDeal, type Deal } from "@/lib/deals/deals";
import { DealFacts } from "./DealFacts";
import { DealTermsForm } from "./DealTermsForm";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/settings/SettingsForms.module.css";

type Action = "submissions" | "approvals" | "rejections" | "cancellations";

function Lines({ deal }: { deal: Deal }) {
  const lines: [string, string][] = [
    ["Vehicle price", formatCurrency(deal.basePrice)],
    [
      deal.promoCode ? `Promo ${deal.promoCode}` : "Promo discount",
      `− ${formatCurrency(deal.promoDiscount)}`,
    ],
    ["Negotiated discount", `− ${formatCurrency(deal.negotiatedDiscount)}`],
    ["Processing fee", formatCurrency(deal.processingFee)],
    ["Road tax", formatCurrency(deal.roadTax)],
    [
      `Service tax (${Number((deal.sstRate * 100).toFixed(2))}% of fee)`,
      formatCurrency(deal.serviceTax),
    ],
    [deal.warrantyName ?? "Warranty", formatCurrency(deal.warrantyPrice)],
    ["Deposit credit", `− ${formatCurrency(deal.depositCredit)}`],
    ["Trade-in allowance", `− ${formatCurrency(deal.tradeInAllowance)}`],
    ["Balance payable", formatCurrency(deal.balancePayable)],
  ];
  return (
    <dl className={workbench.details}>
      {lines.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function DealDrawer({
  selection,
  actor,
  onChanged,
}: {
  selection: ReturnType<typeof useStaffSelection<Deal>>;
  actor: CurrentActor;
  onChanged: () => void;
}) {
  const toast = useToast();
  const terms = useSettingsSave(parseDeal);
  const actions = useSettingsSave(parseDeal);
  const [opened, setOpened] = useState<Deal | null>(null);
  const [revision, setRevision] = useState(0);
  const [reason, setReason] = useState("");
  const [dirty, setDirty] = useState(false);
  const latest = selection.result?.data ?? null;
  // The editor stays based on the first read of this deal until the agent chooses newer values.
  if (latest && opened?.id !== latest.id) {
    setOpened(latest);
    setReason("");
  }
  const base = opened?.id === latest?.id ? opened : null;
  const busy = terms.busy || actions.busy;
  const loading = selection.busy && !latest;
  const can = latest ? dealAbilities(latest, actor) : null;
  const newer = base && latest && latest.version !== base.version;

  async function refreshed() {
    onChanged();
    await selection.choose(selection.id);
    setOpened(null);
    setRevision((value) => value + 1);
    setReason("");
  }

  async function act(action: Action, message: string) {
    if (!latest) {
      return;
    }
    const needsReason = action === "rejections" || action === "cancellations";
    const done = await actions.save(`${dealPath(latest.id)}/${action}`, "POST", {
      expectedVersion: latest.version,
      reason: needsReason ? reason : null,
    });
    if (done) {
      toast.success(message);
      await refreshed();
    }
  }

  function close() {
    if (!busy) {
      setOpened(null);
      selection.close();
    }
  }

  const noteNeeded = Boolean(can?.decide || can?.cancel);
  const reasonOk = validDealReason(reason);
  return (
    <>
      <LoadingProgress label="Loading deal" active={selection.busy} />
      <LoadingProgress label="Saving deal" active={busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        layout="workspace"
        title={latest ? (latest.orderNumber ?? "Draft deal") : "Deal"}
        headerLoading={loading}
        description={latest ? dealStatusLabels[latest.status] : undefined}
        onClose={close}
        closeDisabled={busy}
        headerActions={
          <RefreshButton
            label="Refresh deal"
            busy={selection.busy}
            disabled={!selection.id || busy}
            onClick={() => void selection.choose(selection.id)}
          />
        }
      >
        {loading && <SettingsDrawerLoadingShell fields={4} />}
        {!selection.busy && !latest && (
          <div className={workbench.protocol}>
            <p>The deal is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry deal
            </Button>
          </div>
        )}
        {latest && can && (
          <div className={workshop.workspace}>
            <div className={workshop.summary}>
              <div className={workshop.summaryMain}>
                <div>
                  <span className={workshop.summaryLabel}>Balance payable</span>
                  <strong className={workshop.summaryValue}>
                    {formatCurrency(latest.balancePayable)}
                  </strong>
                </div>
                <div className={workshop.summaryContext}>
                  <strong>{dealStatusLabels[latest.status]}</strong>
                  <span>{latest.orderNumber ?? "Not yet submitted"}</span>
                </div>
              </div>
            </div>
            <div className={workshop.body}>
              <div className={forms.stackTop}>
                <section className={workbench.stack}>
                  <h3>Price</h3>
                  <Lines deal={latest} />
                  {latest.discountLimitAtSubmit !== null && (
                    <p className={workbench.help}>
                      Agent discount limit when submitted:{" "}
                      {formatCurrency(latest.discountLimitAtSubmit)}.
                      {latest.negotiatedDiscount > latest.discountLimitAtSubmit &&
                        ` The negotiated discount is ${formatCurrency(
                          latest.negotiatedDiscount - latest.discountLimitAtSubmit,
                        )} over it.`}
                    </p>
                  )}
                </section>
                <DealFacts deal={latest} />
                {newer && (
                  <div className={workbench.protocol} role="status">
                    <p>A newer version was loaded. Your edits are kept until you use it.</p>
                    <Button
                      size="compact"
                      variant="secondary"
                      onClick={() => {
                        setOpened(latest);
                        setRevision((value) => value + 1);
                      }}
                    >
                      Use latest values
                    </Button>
                  </div>
                )}
                {can.edit && base && (
                  <DealTermsForm
                    key={`${base.id}:${revision}`}
                    base={base}
                    saver={terms}
                    disabled={busy}
                    onSaved={() => void refreshed()}
                    onReload={() => void selection.choose(selection.id)}
                    onDirty={setDirty}
                  />
                )}
                {noteNeeded && (
                  <section className={workbench.stack}>
                    <h3>Decision note</h3>
                    <Textarea
                      label="Reason"
                      labelHidden
                      placeholder="Required to reject or cancel this deal"
                      maxLength={1000}
                      rows={3}
                      value={reason}
                      disabled={busy}
                      onChange={(event) => setReason(event.target.value.replace(/^\s+/, ""))}
                    />
                  </section>
                )}
                {actions.notice && (
                  <div className={workbench.protocol} role="alert">
                    <p>
                      {actions.notice.status === 409
                        ? "This deal changed elsewhere. Refresh it before acting again."
                        : actions.notice.message}
                    </p>
                  </div>
                )}
                <section className={workbench.stack}>
                  <h3>History</h3>
                  <ol className={workbench.stack}>
                    {[...latest.events].reverse().map((event) => (
                      <li key={event.id}>
                        <strong>{dealEventLabels[event.kind] ?? event.kind}</strong>{" "}
                        <span className={workbench.help}>
                          {formatMalaysianDateTime(event.recordedAtUtc)}
                          {latest.context.staff[event.actorId] &&
                            ` · ${latest.context.staff[event.actorId]}`}
                        </span>
                        {event.reason && <div className={workbench.help}>{event.reason}</div>}
                      </li>
                    ))}
                  </ol>
                </section>
              </div>
            </div>
            <StaffWorkspaceFooter
              title={dealStatusLabels[latest.status]}
              description={
                dirty
                  ? "Save your term changes before submitting."
                  : "Actions follow your role and the deal's state."
              }
            >
              {can.submit && (
                <ConfirmationButton
                  confirmationTitle="Submit this deal?"
                  message="Submitting captures today's fee, tax and discount limit. A larger discount goes to a manager."
                  size="compact"
                  disabled={busy || dirty}
                  onConfirm={() => act("submissions", "Deal submitted.")}
                >
                  Submit deal
                </ConfirmationButton>
              )}
              {can.decide && (
                <>
                  <ConfirmationButton
                    confirmationTitle="Approve this deal?"
                    message="The deal becomes approved with its submitted terms."
                    size="compact"
                    disabled={busy}
                    onConfirm={() => act("approvals", "Deal approved.")}
                  >
                    Approve
                  </ConfirmationButton>
                  <ConfirmationButton
                    confirmationTitle="Reject this deal?"
                    message="The deal returns to draft with your reason, and the agent can revise and resubmit."
                    size="compact"
                    variant="secondary"
                    disabled={busy || !reasonOk}
                    onConfirm={() => act("rejections", "Deal returned to draft.")}
                  >
                    Reject
                  </ConfirmationButton>
                </>
              )}
              {can.cancel && (
                <ConfirmationButton
                  confirmationTitle="Cancel this deal?"
                  message="This is final. The vehicle becomes available for another deal."
                  size="compact"
                  variant="danger"
                  disabled={busy || !reasonOk}
                  onConfirm={() => act("cancellations", "Deal cancelled.")}
                >
                  Cancel deal
                </ConfirmationButton>
              )}
            </StaffWorkspaceFooter>
          </div>
        )}
      </Dialog>
    </>
  );
}

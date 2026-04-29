"use client";

import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { Select } from "@/components/ui/Input";
import { useHoldHandling } from "@/hooks/use-hold-handling";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { claimAge } from "@/lib/sales/sales";
import type { ReservationDetail } from "@/lib/vehicles/reservation-detail";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./ReservationDrawer.module.css";

// Who has the hold and the manager's commands for it: give it to an agent, move it or take the claim back.
export function ReservationHandling({
  detail,
  now,
  onChanged,
}: {
  detail: ReservationDetail;
  now: number;
  onChanged: () => void;
}) {
  const { reservation } = detail;
  const claim = reservation.claim;
  const handling = useHoldHandling(reservation.id, onChanged);
  const { command } = handling;
  const assigning = handling.dialog === "assign";
  const noAgents = Boolean(handling.agents) && handling.agents?.length === 0;
  const hours = claim ? Math.floor(claim.frozenRemainingSeconds / 3600) : 0;
  const minutes = claim ? Math.floor((claim.frozenRemainingSeconds % 3600) / 60) : 0;

  return (
    <section className={`${workbench.stack} ${styles.section}`}>
      <LoadingProgress label="Updating the hold" active={command.busy} />
      <h3>Handling</h3>
      <dl className={workbench.details}>
        {claim ? (
          <>
            <div>
              <dt>Handled by</dt>
              <dd>{claim.agent}</dd>
            </div>
            <div>
              <dt>Claimed</dt>
              <dd>
                {formatMalaysianDateTime(claim.claimedAtUtc)} · {claimAge(claim.claimedAtUtc, now)}
              </dd>
            </div>
            <div>
              <dt>Time left</dt>
              <dd>
                {hours}h {minutes}m, paused while claimed
              </dd>
            </div>
          </>
        ) : (
          <div>
            <dt>Handled by</dt>
            <dd>Nobody has claimed this hold</dd>
          </div>
        )}
      </dl>
      <div className={styles.handlingActions}>
        <Button size="compact" variant="secondary" onClick={handling.openAssign}>
          {claim ? "Reassign to another agent" : "Assign to an agent"}
        </Button>
        {claim && (
          <Button size="compact" variant="secondary" onClick={handling.openRelease}>
            Release claim
          </Button>
        )}
      </div>
      <ConfirmationDialog
        open={assigning}
        title={claim ? "Reassign this hold" : "Assign this hold"}
        confirmLabel={claim ? "Reassign hold" : "Assign hold"}
        cancelLabel="Not now"
        busy={command.busy}
        onClose={handling.close}
        onConfirm={handling.assign}
        facts={[
          { label: "Vehicle", value: reservation.vehicle },
          { label: "Buyer", value: reservation.buyer },
        ]}
      >
        <p>
          {claim
            ? "The draft deal moves to the chosen agent, who continues with the customer. The hold's timer stays paused."
            : "Assigning creates the draft deal for the chosen agent and pauses the hold's timer."}
        </p>
        <Select
          label="Sales agent"
          value={handling.agentId}
          disabled={command.busy || !handling.agents}
          onChange={(event) => handling.setAgentId(event.target.value)}
        >
          <option value="">{handling.agents ? "Choose an agent" : "Loading agents…"}</option>
          {handling.agents?.map((agent) => (
            <option key={agent.id} value={agent.id} disabled={agent.email === claim?.agent}>
              {agent.email}
            </option>
          ))}
        </Select>
        {noAgents && <p role="alert">This branch has no active sales agents to assign.</p>}
        {handling.agentsFailed && (
          <p role="alert">
            The agents could not be loaded.{" "}
            <Button size="compact" variant="secondary" onClick={() => void handling.loadAgents()}>
              Retry agents
            </Button>
          </p>
        )}
        {command.notice && <p role="alert">{command.notice.message}</p>}
      </ConfirmationDialog>
      <ConfirmationDialog
        open={handling.dialog === "release"}
        title="Release this claim"
        confirmLabel="Release claim"
        cancelLabel="Keep claim"
        danger
        busy={command.busy}
        onClose={handling.close}
        onConfirm={handling.release}
        facts={[
          { label: "Vehicle", value: reservation.vehicle },
          { label: "Handled by", value: claim?.agent ?? "" },
        ]}
      >
        <p>
          The agent&apos;s draft deal is cancelled and the hold returns to Open with the time it had
          left. A deal that was already submitted must be cancelled as a deal instead.
        </p>
        {command.notice && <p role="alert">{command.notice.message}</p>}
      </ConfirmationDialog>
    </section>
  );
}

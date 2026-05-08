"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Select } from "@/components/ui/Input";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import { privateApiFetch } from "@/lib/api-browser";
import { parseSaleAgents } from "@/lib/sales/parse";
import { salePath } from "@/lib/sales/query";
import type { SaleAgent } from "@/lib/sales/contracts";

// A manager gives an open sale to an agent of its branch, or moves a handled one to another agent.
export function SaleAssignDialog({
  editor,
  open,
  onClose,
}: {
  editor: ReturnType<typeof useSaleEditor>;
  open: boolean;
  onClose: () => void;
}) {
  const { snapshot } = editor;
  const handled = snapshot.sale.agentId !== null;
  const [agents, setAgents] = useState<SaleAgent[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [agentId, setAgentId] = useState("");
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!open) {
      return;
    }
    const cancellation = new AbortController();
    privateApiFetch(`${salePath(snapshot.id)}/agents`, parseSaleAgents, {
      signal: cancellation.signal,
    })
      .then((result) => {
        if (result.status === 200 && result.data) {
          setAgents(result.data);
          setFailed(false);
        } else {
          setFailed(true);
        }
      })
      .catch(() => {
        if (!cancellation.signal.aborted) {
          setFailed(true);
        }
      });
    return () => cancellation.abort();
  }, [open, snapshot.id, attempt]);

  async function assign() {
    if (agentId && (await editor.run("assignments", { agentId }))) {
      setAgentId("");
      onClose();
    }
  }

  return (
    <ConfirmationDialog
      open={open}
      title={handled ? "Reassign this sale" : "Assign this sale"}
      confirmLabel={handled ? "Reassign sale" : "Assign sale"}
      cancelLabel="Not now"
      busy={editor.busy}
      locked={editor.uncertain === "assignments"}
      onClose={onClose}
      onConfirm={assign}
      facts={[
        { label: "Vehicle", value: snapshot.context.vehicle },
        { label: "Customer", value: snapshot.context.customer },
      ]}
    >
      <p>
        {handled
          ? "The sale moves to the chosen agent with its terms and status. The previous agent loses access."
          : "The chosen agent starts handling the sale and its timer pauses."}
      </p>
      <Select
        label="Sales agent"
        value={agentId}
        disabled={editor.busy || !agents}
        onChange={(event) => setAgentId(event.target.value)}
      >
        <option value="">{agents ? "Choose an agent" : "Loading agents…"}</option>
        {agents?.map((agent) => (
          <option key={agent.id} value={agent.id} disabled={agent.id === snapshot.sale.agentId}>
            {agent.email}
          </option>
        ))}
      </Select>
      {agents?.length === 0 && (
        <p role="alert">This branch has no active sales agents to assign.</p>
      )}
      {failed && (
        <p role="alert">
          The agents could not be loaded.{" "}
          <Button
            size="compact"
            variant="secondary"
            onClick={() => setAttempt((value) => value + 1)}
          >
            Retry agents
          </Button>
        </p>
      )}
    </ConfirmationDialog>
  );
}

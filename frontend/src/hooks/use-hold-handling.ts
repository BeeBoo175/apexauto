"use client";

import { useEffect, useRef, useState } from "react";
import { useSettingsSave } from "./use-settings-save";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { parseDeal } from "@/lib/deals/deals";
import { parseHoldAgents, type HoldAgent } from "@/lib/sales/hold-agents";
import { holdsPath } from "@/lib/sales/sales";

type HandlingDialog = "assign" | "release" | null;

// A manager gives a hold to an agent, moves it to another or gives the claim back; each confirmation carries its own operation identity.
export function useHoldHandling(reservationId: string, onChanged: () => void) {
  const [dialog, setDialog] = useState<HandlingDialog>(null);
  const [agents, setAgents] = useState<HoldAgent[] | null>(null);
  const [agentsFailed, setAgentsFailed] = useState(false);
  const [agentId, setAgentId] = useState("");
  const [operation, setOperation] = useState("");
  const controller = useRef<AbortController | null>(null);
  const command = useSettingsSave(parseDeal);
  const toast = useToast();

  useEffect(() => () => controller.current?.abort(), []);

  async function loadAgents() {
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setAgentsFailed(false);
    try {
      const result = await privateApiFetch(`${holdsPath(reservationId)}/agents`, parseHoldAgents, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data) {
        throw new Error("Agents unavailable");
      }
      setAgents(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        setAgentsFailed(true);
      }
    }
  }

  function openAssign() {
    command.clearNotice();
    setAgentId("");
    setOperation(crypto.randomUUID());
    setDialog("assign");
    void loadAgents();
  }

  function openRelease() {
    command.clearNotice();
    setDialog("release");
  }

  function close() {
    controller.current?.abort();
    setDialog(null);
  }

  async function assign() {
    if (!agentId) {
      return;
    }
    const deal = await command.save(`${holdsPath(reservationId)}/assignments`, "POST", {
      id: operation,
      agentId,
    });
    if (deal) {
      toast.success("The hold was given to the agent.");
      close();
      onChanged();
    }
  }

  async function release() {
    const deal = await command.save(`${holdsPath(reservationId)}/releases`, "POST", {});
    if (deal) {
      toast.success("Claim released. The hold is back in the queue with the time it had left.");
      close();
      onChanged();
    }
  }

  return {
    dialog,
    agents,
    agentsFailed,
    agentId,
    setAgentId,
    command,
    openAssign,
    openRelease,
    close,
    loadAgents,
    assign,
    release,
  };
}

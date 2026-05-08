"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord } from "@/lib/auth/contracts";
import {
  parseSale,
  parseSaleReceipt,
  salePath,
  type Sale,
  type SaleReceipt,
} from "@/lib/sales/sale";
import { useApiCommand } from "./use-api-command";
import { useStaffNavigationLock } from "./use-staff-navigation-lock";

export type SaleCommandName =
  | "claims"
  | "assignments"
  | "releases"
  | "terms"
  | "submissions"
  | "approvals"
  | "rejections"
  | "cancellations";

// A receipt belongs to the captured command only if it names the same operation and sale.
const receiptMatches = (saleId: string) => (receipt: SaleReceipt, request: unknown) =>
  isRecord(request) && receipt.operationId === request.operationId && receipt.saleId === saleId;

// The sale workspace's confirmed record, its commands and their recovery, as the workshop editor keeps its job.
export function useSaleEditor(initial: Sale, onChanged: () => void) {
  const id = initial.id;
  const toast = useToast();
  const [entry, setEntry] = useState(initial);
  const [snapshot, setSnapshot] = useState(initial);
  // The version the open terms form started from; a newer confirmed version is offered, never forced on it.
  const [base, setBase] = useState(initial);
  const [formDirty, setFormDirty] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const matches = receiptMatches(id);
  const path = (command: SaleCommandName) => `${salePath(id)}/${command}`;
  const commands = {
    claims: useApiCommand(path("claims"), parseSaleReceipt, matches, privateApiFetch),
    assignments: useApiCommand(path("assignments"), parseSaleReceipt, matches, privateApiFetch),
    releases: useApiCommand(path("releases"), parseSaleReceipt, matches, privateApiFetch),
    terms: useApiCommand(path("terms"), parseSaleReceipt, matches, privateApiFetch),
    submissions: useApiCommand(path("submissions"), parseSaleReceipt, matches, privateApiFetch),
    approvals: useApiCommand(path("approvals"), parseSaleReceipt, matches, privateApiFetch),
    rejections: useApiCommand(path("rejections"), parseSaleReceipt, matches, privateApiFetch),
    cancellations: useApiCommand(path("cancellations"), parseSaleReceipt, matches, privateApiFetch),
  };
  const all = Object.values(commands);
  const busy = all.some((command) => command.busy);
  const uncertain = (Object.keys(commands) as SaleCommandName[]).find(
    (name) => commands[name].uncertain,
  );
  const operating = busy || uncertain !== undefined;
  useStaffNavigationLock(formDirty || operating);

  // A fresh read from the host replaces the confirmed record; the form keeps its base until it is clean.
  if (entry !== initial) {
    setEntry(initial);
    setSnapshot(initial);
    if (!formDirty) {
      setBase(initial);
    }
  }

  useEffect(() => () => controller.current?.abort(), []);

  async function refresh(afterCommand = false) {
    if (controller.current || (operating && !afterCommand)) {
      return false;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setRefreshing(true);
    try {
      const result = await privateApiFetch(salePath(id), parseSale, { signal: attempt.signal });
      if (attempt.signal.aborted) {
        return false;
      }
      if (result.status !== 200 || result.data?.id !== id) {
        throw new Error("Sale unavailable");
      }
      setSnapshot(result.data);
      setConfirmed(false);
      if (!formDirty || afterCommand) {
        setBase(result.data);
      }
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "The sale could not be refreshed. Confirmed details and your entries remain shown.",
        );
      }
      return false;
    } finally {
      if (!attempt.signal.aborted) {
        setRefreshing(false);
      }
      controller.current = null;
    }
  }

  // Each command names a fresh operation and the confirmed version; an unconfirmed one replays exactly.
  async function run(name: SaleCommandName, fields: Record<string, unknown> = {}) {
    const command = commands[name];
    if (busy || (uncertain && uncertain !== name)) {
      return false;
    }
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      expectedVersion: snapshot.sale.version,
      ...fields,
    });
    if (result?.status !== 200 || !result.data) {
      if (result?.status === 409) {
        void refresh(true);
      }
      return false;
    }
    if (name === "terms") {
      setFormDirty(false);
    }
    onChanged();
    const read = await refresh(true);
    setConfirmed(!read);
    return true;
  }

  return {
    snapshot,
    base,
    // The open form started from an older version than the one now confirmed.
    stale: formDirty && base.sale.version !== snapshot.sale.version,
    dirty: formDirty,
    markDirty: setFormDirty,
    useLatest: () => setBase(snapshot),
    refreshing,
    confirmed,
    busy,
    uncertain,
    operating,
    refresh,
    run,
  };
}

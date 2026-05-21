"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord } from "@/lib/auth/contracts";
import {
  normalizeSaleReason,
  parseSale,
  parseSaleOptions,
  parseSaleReceipt,
} from "@/lib/sales/parse";
import { salePath } from "@/lib/sales/query";
import type { Sale, SaleCommand, SaleOptions, SaleReceipt } from "@/lib/sales/contracts";
import { saleTermsChanged, saleTermsDraft, type SaleTermsDraft } from "@/lib/sales/sale-terms";
import { useApiCommand } from "./use-api-command";
import { useEditGuard } from "./use-edit-guard";
import type { SalePaymentEvidence, SaleHandoverEvidence } from "@/lib/sales/completion-evidence";
import type { SaleSettlementReview } from "@/lib/sales/documents";

type NoFields = Record<string, never>;

export interface SaleTermsFields {
  warrantyPackageId: string | null;
  negotiatedDiscount: number;
  tradeInAppointmentId: string | null;
  tradeInAllowance: number;
}

// What each command sends besides its operation and base version.
export interface SaleCommandFields {
  claims: NoFields;
  assignments: { agentId: string };
  releases: NoFields;
  "term-revisions": SaleTermsFields;
  submissions: NoFields;
  approvals: NoFields;
  rejections: { reason: string };
  cancellations: { reason: string };
  settlements: { payment: SalePaymentEvidence; review: SaleSettlementReview };
  handovers: { evidence: SaleHandoverEvidence };
}

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
  // The terms and decision note being typed live here, so leaving a section never loses them.
  const [terms, setTerms] = useState(() => saleTermsDraft(initial.sale));
  const [termsBase, setTermsBase] = useState(initial);
  // The draft a save sent stays clean until the confirmed version it produced becomes the base.
  const [savedTerms, setSavedTerms] = useState<SaleTermsDraft | null>(null);
  const [reason, setReason] = useState("");
  const [cancellationReason, setCancellationReason] = useState("");
  const [options, setOptions] = useState<SaleOptions | null>(null);
  const [optionsFailed, setOptionsFailed] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const matches = receiptMatches(id);
  const path = (command: SaleCommand) => `${salePath(id)}/${command}`;
  const commands = {
    claims: useApiCommand(path("claims"), parseSaleReceipt, matches, privateApiFetch),
    assignments: useApiCommand(path("assignments"), parseSaleReceipt, matches, privateApiFetch),
    releases: useApiCommand(path("releases"), parseSaleReceipt, matches, privateApiFetch),
    "term-revisions": useApiCommand(
      path("term-revisions"),
      parseSaleReceipt,
      matches,
      privateApiFetch,
    ),
    submissions: useApiCommand(path("submissions"), parseSaleReceipt, matches, privateApiFetch),
    approvals: useApiCommand(path("approvals"), parseSaleReceipt, matches, privateApiFetch),
    rejections: useApiCommand(path("rejections"), parseSaleReceipt, matches, privateApiFetch),
    cancellations: useApiCommand(path("cancellations"), parseSaleReceipt, matches, privateApiFetch),
    settlements: useApiCommand(path("settlements"), parseSaleReceipt, matches, privateApiFetch),
    handovers: useApiCommand(path("handovers"), parseSaleReceipt, matches, privateApiFetch),
  };
  const all = Object.values(commands);
  const busy = all.some((command) => command.busy);
  const uncertain = (Object.keys(commands) as SaleCommand[]).find(
    (name) => commands[name].uncertain,
  );
  const operating = busy || uncertain !== undefined;
  const keysTerms = snapshot.actions.saveTerms;
  const formDirty = keysTerms && terms !== savedTerms && saleTermsChanged(terms, base.sale);
  const notesDirty = Boolean(reason.trim() || cancellationReason.trim());
  const editingTerms = formDirty || uncertain === "term-revisions";
  useEditGuard({
    dirty: formDirty || notesDirty,
    pending: busy,
    uncertain: uncertain !== undefined,
    label: "Sale changes",
    discard: () => {
      setTerms(saleTermsDraft(base.sale));
      setSavedTerms(null);
      setReason("");
      setCancellationReason("");
    },
  });

  // A fresh read from the host replaces the confirmed record; the form keeps its base until it is clean.
  if (entry !== initial) {
    setEntry(initial);
    setSnapshot(initial);
    if (!formDirty) {
      setBase(initial);
    }
  }

  // A new base restarts the draft from its terms.
  if (termsBase !== base) {
    setTermsBase(base);
    setTerms(saleTermsDraft(base.sale));
    setSavedTerms(null);
  }

  useEffect(() => () => controller.current?.abort(), []);

  // The warranty and trade-in choices are read once per sale, when its agent may key terms.
  useEffect(() => {
    if (!keysTerms) {
      return;
    }
    const attempt = new AbortController();
    privateApiFetch(`${salePath(id)}/options`, parseSaleOptions, { signal: attempt.signal })
      .then((result) => {
        if (result.status === 200 && result.data) {
          setOptions(result.data);
          setOptionsFailed(false);
        } else {
          setOptionsFailed(true);
        }
      })
      .catch(() => {
        if (!attempt.signal.aborted) {
          setOptionsFailed(true);
        }
      });
    return () => attempt.abort();
  }, [id, keysTerms]);

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
  async function run<Name extends SaleCommand>(
    name: Name,
    fields: SaleCommandFields[Name],
    expectedVersion = snapshot.sale.version,
  ) {
    const command = commands[name];
    if (busy || (uncertain && uncertain !== name)) {
      return false;
    }
    const normalized = "reason" in fields ? normalizeSaleReason(fields.reason) : null;
    if ("reason" in fields && !normalized && !command.uncertain) {
      toast.error("Enter a reason of 1 to 1000 characters without control characters.");
      return false;
    }
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      expectedVersion,
      ...fields,
      ...("reason" in fields ? { reason: normalized } : {}),
    });
    if (result?.status !== 200 || !result.data) {
      if (result?.status === 409) {
        void refresh(true);
      }
      return false;
    }
    if (name === "term-revisions") {
      setSavedTerms(terms);
    }
    if (name === "rejections") {
      setReason("");
    }
    if (name === "cancellations") {
      setCancellationReason("");
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
    notesDirty,
    locked: formDirty || notesDirty || operating,
    terms,
    changeTerms: (patch: Partial<SaleTermsDraft>) => setTerms((draft) => ({ ...draft, ...patch })),
    reason,
    setReason,
    cancellationReason,
    setCancellationReason,
    discardNotes: () => {
      setReason("");
      setCancellationReason("");
    },
    editingTerms,
    discardTerms: () => {
      setTerms(saleTermsDraft(base.sale));
      setSavedTerms(null);
    },
    options,
    optionsFailed,
    useLatest: () => setBase(snapshot),
    refreshing,
    confirmed,
    busy,
    uncertain,
    operating,
    commandBlocked: (name: SaleCommand) =>
      busy || refreshing || confirmed || (uncertain !== undefined && uncertain !== name),
    refresh,
    run,
  };
}

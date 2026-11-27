"use client";

import { useContext, useEffect, useRef, useState } from "react";
import { StaffActorContext } from "@/components/staff/StaffActorBoundary";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import { salePath } from "@/lib/sales/query";
import { parseMyr } from "@/lib/currency-input";
import {
  boundedText,
  isPaymentDate,
  type PaymentMethod,
  type SalePaymentEvidence,
} from "@/lib/sales/completion-evidence";
import { parseSettlementReview, type SaleSettlementReview } from "@/lib/sales/documents";
import type { useSaleEditor } from "./use-sale-editor";
import { useEditGuard } from "./use-edit-guard";

const emptyPayment = {
  method: "Cash" as PaymentMethod,
  received: "",
  reference: "",
  date: "",
  bank: "",
  financing: "",
  lou: "",
  louVerified: false,
  zeroConfirmed: false,
};
const emptyHandover = {
  odometer: "",
  keys: "",
  customer: false,
  ownership: false,
  insurance: false,
};

export function useSaleCompletion(editor: ReturnType<typeof useSaleEditor>) {
  const actor = useContext(StaffActorContext);
  const toast = useToast();
  const { snapshot } = editor;
  const [stage, setStage] = useState<"settlement" | "handover" | null>(null);
  const [version, setVersion] = useState(snapshot.sale.version);
  const [review, setReview] = useState<SaleSettlementReview | null>(null);
  const [payment, setPayment] = useState(emptyPayment);
  const [handover, setHandover] = useState(emptyHandover);
  const [reviewed, setReviewed] = useState(false);
  const [loading, setLoading] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const own = actor?.role === "SalesAgent" && actor.userId === snapshot.sale.agentId;
  const eligible = own && ["Approved", "Settled"].includes(snapshot.sale.status);
  const command = stage === "handover" ? "handovers" : "settlements";
  const uncertain = editor.uncertain === command;
  const stale = stage !== null && version !== snapshot.sale.version;
  const dirty = stage !== null;
  const blocked = editor.commandBlocked(command) || editor.dirty || editor.notesDirty || loading;

  function discard() {
    setStage(null);
    setReview(null);
    setPayment(emptyPayment);
    setHandover(emptyHandover);
    setReviewed(false);
  }

  useEditGuard({
    dirty,
    pending: loading || editor.busy,
    uncertain,
    label: "Settlement and handover",
    discard,
  });
  useEffect(() => () => controller.current?.abort(), []);

  async function start() {
    if (!eligible || blocked || controller.current) {
      return;
    }
    setVersion(snapshot.sale.version);
    setReviewed(false);
    if (snapshot.sale.status === "Settled") {
      setHandover({ ...emptyHandover, odometer: String(snapshot.context.mileageKilometres) });
      setStage("handover");
      return;
    }
    const cancellation = new AbortController();
    controller.current = cancellation;
    setLoading(true);
    try {
      const response = await privateApiFetch(
        `${salePath(snapshot.id)}/settlement-review`,
        parseSettlementReview,
        {
          signal: AbortSignal.any([cancellation.signal, AbortSignal.timeout(22_000)]),
        },
      );
      if (cancellation.signal.aborted) {
        return;
      }
      if (response.status !== 200 || !response.data) {
        toast.error(response.message);
        return;
      }
      setReview(response.data);
      setPayment({
        ...emptyPayment,
        method: snapshot.sale.balancePayable === 0 ? "ZeroBalance" : "Cash",
      });
      setStage("settlement");
    } catch {
      if (!cancellation.signal.aborted) {
        toast.error("Buyer details could not be loaded. Reload the page to try again.");
      }
    } finally {
      controller.current = null;
      if (!cancellation.signal.aborted) {
        setLoading(false);
      }
    }
  }

  function paymentEvidence(): SalePaymentEvidence | null {
    const result: SalePaymentEvidence = {
      method: payment.method,
      receivedAmount: 0,
      paymentReference: null,
      paymentDate: null,
      bank: null,
      financingAmount: 0,
      louReference: null,
      louVerified: false,
      zeroBalanceConfirmed: false,
    };
    if (payment.method === "ZeroBalance") {
      return payment.zeroConfirmed ? { ...result, zeroBalanceConfirmed: true } : null;
    }
    const receivedAmount = parseMyr(payment.received);
    if (receivedAmount === null) {
      return null;
    }
    if (payment.method === "HirePurchase") {
      const financingAmount = parseMyr(payment.financing);
      return financingAmount !== null &&
        boundedText(payment.bank, 100) &&
        boundedText(payment.lou, 1000) &&
        payment.louVerified
        ? {
            ...result,
            receivedAmount,
            financingAmount,
            bank: payment.bank.trim(),
            louReference: payment.lou.trim(),
            louVerified: true,
          }
        : null;
    }
    return boundedText(payment.reference, 1000) && isPaymentDate(payment.date)
      ? {
          ...result,
          receivedAmount,
          paymentReference: payment.reference.trim(),
          paymentDate: payment.date,
        }
      : null;
  }

  const evidence = paymentEvidence();
  const odometer = /^\d+$/.test(handover.odometer) ? Number(handover.odometer) : NaN;
  const keys = /^\d+$/.test(handover.keys) ? Number(handover.keys) : NaN;
  const handoverValid =
    Number.isSafeInteger(odometer) &&
    odometer <= 2147483647 &&
    Number.isSafeInteger(keys) &&
    keys <= 2147483647 &&
    handover.customer &&
    handover.ownership &&
    handover.insurance;
  const reviewValid =
    review &&
    boundedText(review.buyerName, 200) &&
    boundedText(review.buyerAddress, 1000) &&
    reviewed;
  const valid = stage === "settlement" ? Boolean(evidence && reviewValid) : handoverValid;

  async function submit() {
    if (blocked || (!uncertain && (!valid || stale || !eligible))) {
      return;
    }
    let confirmed = false;
    if (stage === "settlement" && review && evidence) {
      confirmed = await editor.run(
        "settlements",
        {
          payment: evidence,
          review: {
            ...review,
            buyerName: review.buyerName.trim(),
            buyerAddress: review.buyerAddress.trim(),
          },
        },
        version,
      );
    }
    if (stage === "handover") {
      confirmed = await editor.run(
        "handovers",
        {
          evidence: {
            odometerKilometres: odometer,
            keysDelivered: keys,
            customerAcknowledged: handover.customer,
            ownershipTransferCompleted: handover.ownership,
            insuranceConfirmed: handover.insurance,
          },
        },
        version,
      );
    }
    if (confirmed) {
      discard();
    }
  }

  return {
    stage,
    review,
    payment,
    handover,
    reviewed,
    setReviewed,
    loading,
    eligible,
    dirty,
    stale,
    uncertain,
    valid,
    blocked,
    start,
    submit,
    discard,
    changeReview: (patch: Partial<Pick<SaleSettlementReview, "buyerName" | "buyerAddress">>) => {
      setReview((current) => (current ? { ...current, ...patch } : null));
      setReviewed(false);
    },
    changePayment: (patch: Partial<typeof emptyPayment>) =>
      setPayment((current) => ({ ...current, ...patch })),
    changeHandover: (patch: Partial<typeof emptyHandover>) =>
      setHandover((current) => ({ ...current, ...patch })),
  };
}

import { notFound, redirect } from "next/navigation";
import { readCurrentActor } from "@/lib/auth/server";
import { actorDestination } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import {
  readCustomerSaleDocuments,
  readCustomerSaleReport,
} from "@/lib/sales/customer-documents-server";
import { AccountSaleDocuments } from "@/components/account/AccountSaleDocuments";

export const metadata = {
  title: "Your sale documents | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function SaleDocumentsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  const current = await readCurrentActor();
  if (current.status === 401) {
    redirect(`/login?returnTo=${encodeURIComponent(`/account/sales/${id}/documents`)}`);
  }
  if (!current.actor) {
    throw new Error("Account session unavailable");
  }
  if (current.actor.role !== "Customer") {
    redirect(actorDestination(current.actor));
  }
  const documents = await readCustomerSaleDocuments(id, current.actor.userId);
  if ([403, 404].includes(documents.status)) {
    notFound();
  }
  const delivered = documents.data?.some(
    (document) => document.contents.sale.status === "Delivered",
  );
  const report = delivered ? await readCustomerSaleReport(id) : null;
  return (
    <AccountSaleDocuments
      actor={current.actor}
      saleId={id}
      documents={documents.data}
      report={report?.data ?? null}
      reportUnavailable={Boolean(delivered && !report?.data)}
    />
  );
}

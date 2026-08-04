import { notFound } from "next/navigation";
import { SalesPageEntry } from "@/components/sales/SalesPageEntry";
import { saleSelectionRoute } from "@/lib/sales/sales-location";
import type { SearchParams } from "@/lib/query";

export default async function SelectedSalePage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; section?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id, section } = await params;
  const selection = saleSelectionRoute(id, section);
  if (!selection) {
    notFound();
  }
  return <SalesPageEntry selection={selection} searchParams={searchParams} />;
}

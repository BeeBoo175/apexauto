import { SalesPageEntry } from "@/components/sales/SalesPageEntry";
import type { SearchParams } from "@/lib/query";

export default function SalesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <SalesPageEntry searchParams={searchParams} />;
}

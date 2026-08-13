import { SalesPageEntry } from "@/components/sales/SalesPageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/sales");

export default function SalesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <SalesPageEntry searchParams={searchParams} />;
}

import { ListingPageEntry } from "@/components/vehicles/ListingPageEntry";
import type { SearchParams } from "@/lib/query";

export default function ManagementPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <ListingPageEntry searchParams={searchParams} />;
}

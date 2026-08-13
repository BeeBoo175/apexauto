import { ListingPageEntry } from "@/components/vehicles/ListingPageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/listings");

export default function ManagementPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <ListingPageEntry searchParams={searchParams} />;
}

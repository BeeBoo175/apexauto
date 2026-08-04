import { notFound } from "next/navigation";
import { ListingPageEntry } from "@/components/vehicles/ListingPageEntry";
import { listingSelectionRoute } from "@/lib/vehicles/listing-location";
import type { SearchParams } from "@/lib/query";

export default async function SelectedListingPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; section?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id, section } = await params;
  const selection = listingSelectionRoute(id, section);
  if (!selection) {
    notFound();
  }
  return <ListingPageEntry selection={selection} searchParams={searchParams} />;
}

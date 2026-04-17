import type { Metadata } from "next";
import { VehicleCatalogue } from "@/components/vehicles/VehicleCatalogue";
import { LoadingProgressProvider } from "@/components/ui/LoadingProgress";
import { requestApiJson } from "@/lib/server/http/api";
import { parseBranch } from "@/lib/branches";
import { parsePage } from "@/lib/api";
import type { SearchParams } from "@/lib/query";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueParams, catalogueQuery } from "@/lib/vehicles/query";

export const metadata: Metadata = {
  title: "Used cars for sale | ApexAuto",
  description:
    "Browse inspected used cars across ApexAuto showrooms. Compare vehicle prices, mileage and specifications.",
  openGraph: {
    title: "Find your next car | ApexAuto",
    description: "Explore inspected vehicles approved for our showroom.",
    type: "website",
  },
};

export default async function VehiclesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const { filters, page } = catalogueParams(await searchParams);
  const query = catalogueQuery(filters, page);
  const [catalogue, branches] = await Promise.all([
    requestApiJson(`/api/vehicles?${query}`, parseCatalogue),
    requestApiJson("/api/branches?page=1&pageSize=25", (value) => parsePage(value, parseBranch)),
  ]);
  return (
    <LoadingProgressProvider initialLabel="Loading vehicles">
      <VehicleCatalogue
        key={query}
        initial={catalogue}
        filters={filters}
        page={page}
        branches={branches.data}
      />
    </LoadingProgressProvider>
  );
}

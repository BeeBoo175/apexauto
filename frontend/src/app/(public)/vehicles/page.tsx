import type { Metadata } from "next";
import { LoadingProgressProvider } from "@/components/ui/LoadingProgress";
import { VehicleCatalogue } from "@/components/vehicles/VehicleCatalogue";
import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import { parseTaxonomy } from "@/lib/intake/parse";
import type { SearchParams } from "@/lib/query";
import { requestApiJson } from "@/lib/server/http/api";
import { CATALOGUE_PAGE_SIZE } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueParams, catalogueQuery } from "@/lib/vehicles/query";

export const metadata: Metadata = {
  title: "Used cars for sale | ApexAuto",
  description:
    "Browse 210-point inspected used cars across ApexAuto Malaysian showrooms. Compare verified mileage, transparent MYR pricing and reserve online.",
  openGraph: {
    title: "Used cars for sale | ApexAuto",
    description: "Explore inspected vehicles approved across ApexAuto Malaysian showrooms.",
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
  const [catalogue, branches, taxonomy] = await Promise.all([
    requestApiJson(`/api/vehicles?${query}&pageSize=${CATALOGUE_PAGE_SIZE}`, parseCatalogue),
    requestApiJson("/api/branches?page=1&pageSize=25", (value) => parsePage(value, parseBranch)),
    requestApiJson("/api/taxonomy", parseTaxonomy),
  ]);

  return (
    <LoadingProgressProvider initialLabel="Loading vehicles">
      <VehicleCatalogue
        key={query}
        initial={catalogue}
        filters={filters}
        page={page}
        branches={branches.data}
        taxonomy={taxonomy.data}
      />
    </LoadingProgressProvider>
  );
}

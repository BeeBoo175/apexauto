import type { Metadata } from "next";
import { VehicleCatalogue } from "@/components/vehicles/VehicleCatalogue";
import { readShowrooms } from "@/lib/guide/guide-reads";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";
import { parseTaxonomy } from "@/lib/intake/parse";
import type { SearchParams } from "@/lib/query";
import { requestApiJson } from "@/lib/server/http/api";
import { CATALOGUE_LISTING_PAGE_SIZE } from "@/lib/vehicles/contracts";
import { RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueParams, catalogueQuery } from "@/lib/vehicles/query";

export const metadata: Metadata = {
  title: "Used cars for sale | ApexAuto",
  description:
    "Browse 210-point inspected used cars across ApexAuto Malaysian showrooms. Compare verified mileage, transparent RM pricing and reserve online.",
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
  const [catalogue, branches, taxonomy, guide] = await Promise.all([
    requestApiJson(
      `/api/vehicles?${query}&pageSize=${CATALOGUE_LISTING_PAGE_SIZE}`,
      parseCatalogue,
    ),
    readShowrooms(),
    requestApiJson("/api/taxonomy", parseTaxonomy),
    requestApiJson("/api/purchase-guide", parsePurchaseGuide),
  ]);

  return (
    <VehicleCatalogue
      key={query}
      initial={catalogue}
      filters={filters}
      page={page}
      branches={branches.data}
      taxonomy={taxonomy.data}
      warranties={guide.data?.warranties ?? null}
      holdHours={guide.data?.reservationHoldHours ?? RESERVATION_HOLD_HOURS}
    />
  );
}

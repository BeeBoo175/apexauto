import type { Metadata } from "next";
import { VehicleCatalogue } from "@/components/vehicles/VehicleCatalogue";
import { apiRead } from "@/lib/server/http/api";
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
    apiRead(`/api/vehicles?${query}`, parseCatalogue),
    apiRead("/api/branches?page=1&pageSize=25", (value) => parsePage(value, parseBranch)),
  ]);
  return (
    <VehicleCatalogue
      key={query}
      initial={catalogue}
      filters={filters}
      page={page}
      branches={branches.data}
    />
  );
}

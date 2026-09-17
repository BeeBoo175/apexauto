import { cache } from "react";
import type { Metadata } from "next";
import { VehicleDetail } from "@/components/vehicles/VehicleDetail";
import { parsePage } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import { parseBranch } from "@/lib/branches";
import { resolveShowrooms } from "@/lib/guide/showrooms";
import { requestApiJson } from "@/lib/server/http/api";
import { catalogueDefaults, catalogueQuery } from "@/lib/vehicles/query";
import { CATALOGUE_PAGE_SIZE } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";
import { readCustomerVehicleAccess } from "@/lib/vehicles/customer-access-server";

type Props = {
  params: Promise<{ id: string }>;
};

const MORE_CAR_COUNT = 10;

// Metadata and content share only this request's bounded, fresh snapshot.
const readDetail = cache(async (id: string) => {
  if (!isUuid(id)) {
    return { status: 404, data: null, message: "Vehicle unavailable.", uncertain: false };
  }
  return requestApiJson(`/api/vehicles/${id}`, (value) => parsePublicDetail(value, id));
});

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  const result = await readDetail(id);
  if (!result.data) {
    return { title: "Vehicle unavailable | ApexAuto", robots: { index: false, follow: false } };
  }
  const { listing, description } = result.data;
  const title = `${listing.manufactureYear} ${listing.make} ${listing.model} ${listing.trim} | ApexAuto`;
  return {
    title,
    description,
    openGraph: { title, description, type: "website" },
  };
}

export default async function VehiclePage({ params }: Props) {
  const { id } = await params;
  const [initial, reservationAccess] = await Promise.all([
    readDetail(id),
    readCustomerVehicleAccess(),
  ]);

  // The showroom block and the car carousels read beside the detail; a failed read only omits its block.
  const listing = initial.data?.listing;
  const [branches, catalogue, branchCatalogue] = listing
    ? await Promise.all([
        requestApiJson("/api/branches?page=1&pageSize=25", (value) =>
          parsePage(value, parseBranch),
        ),
        requestApiJson(
          `/api/vehicles?${catalogueQuery(catalogueDefaults, 1)}&pageSize=${CATALOGUE_PAGE_SIZE}`,
          parseCatalogue,
        ),
        requestApiJson(
          `/api/vehicles?${catalogueQuery({ ...catalogueDefaults, branchId: listing.branchId }, 1)}&pageSize=${CATALOGUE_PAGE_SIZE}`,
          parseCatalogue,
        ),
      ])
    : [null, null, null];

  const showroom =
    resolveShowrooms(branches?.data?.items ?? []).find((item) => item.id === listing?.branchId) ??
    null;
  // One carousel: cars at this showroom first, then the rest of the catalogue, never this car twice.
  const branchCars = (branchCatalogue?.data?.items ?? []).filter((vehicle) => vehicle.id !== id);
  const branchCarIds = new Set(branchCars.map((vehicle) => vehicle.id));
  const otherCars = (catalogue?.data?.items ?? []).filter(
    (vehicle) => vehicle.id !== id && !branchCarIds.has(vehicle.id),
  );
  const moreCars = [...branchCars, ...otherCars].slice(0, MORE_CAR_COUNT);

  return (
    <VehicleDetail
      key={id}
      id={id}
      initial={initial}
      reservationAccess={reservationAccess}
      showroom={showroom}
      moreCars={moreCars}
    />
  );
}

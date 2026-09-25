import { cache } from "react";
import type { Metadata } from "next";
import { VehicleDetail } from "@/components/vehicles/VehicleDetail";
import { isUuid } from "@/lib/auth/contracts";
import { readShowrooms } from "@/lib/guide/guide-reads";
import { requestApiJson } from "@/lib/server/http/api";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";
import { readCustomerVehicleAccess } from "@/lib/vehicles/customer-access-server";

type Props = {
  params: Promise<{ id: string }>;
};

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
  // The showroom block reads beside the detail; a failed read only omits it. The car carousel loads in the browser.
  const [initial, reservationAccess, branches] = await Promise.all([
    readDetail(id),
    readCustomerVehicleAccess(),
    isUuid(id) ? readShowrooms() : null,
  ]);

  const listing = initial.data?.listing;
  const showroom = branches?.showrooms.find((item) => item.id === listing?.branchId) ?? null;

  return (
    <VehicleDetail
      key={id}
      id={id}
      initial={initial}
      reservationAccess={reservationAccess}
      showroom={showroom}
    />
  );
}

import { cache } from "react";
import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { VehicleDetail } from "@/components/vehicles/VehicleDetail";
import { LoadingProgressProvider } from "@/components/ui/LoadingProgress";
import { isUuid } from "@/lib/auth/contracts";
import { requestApiJson } from "@/lib/server/http/api";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";
import { vehicleDetailSectionHref } from "@/lib/vehicles/query";
import type { SearchParams } from "@/lib/query";
import { readCustomerVehicleAccess } from "@/lib/vehicles/customer-access-server";

type Props = {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
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

export default async function VehiclePage({ params, searchParams }: Props) {
  const { id } = await params;
  const sectionHref = vehicleDetailSectionHref(id, await searchParams);
  if (sectionHref) {
    redirect(sectionHref);
  }
  const [initial, reservationAccess] = await Promise.all([
    readDetail(id),
    readCustomerVehicleAccess(),
  ]);
  return (
    <LoadingProgressProvider initialLabel="Loading trade-in options">
      <VehicleDetail key={id} id={id} initial={initial} reservationAccess={reservationAccess} />
    </LoadingProgressProvider>
  );
}

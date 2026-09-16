import type { Metadata } from "next";
import { ApexShieldGuide } from "@/components/guide/ApexShieldGuide";
import { StatusPage } from "@/components/ui/StatusPage";
import { faqEntries } from "@/lib/guide/faq-items";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";
import { CATALOGUE_PAGE_SIZE } from "@/lib/vehicles/contracts";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueDefaults, catalogueQuery } from "@/lib/vehicles/query";

export const metadata: Metadata = {
  title: "ApexShield Warranty | ApexAuto Malaysia",
  description:
    "Compare the ApexShield warranty plans and see which ones a car's inspection grade can take. Optional cover, billed separately from the on-the-road price.",
};

const CAROUSEL_CAR_COUNT = 10;

export default async function WarrantyPage() {
  const [purchase, inspection, catalogue] = await Promise.all([
    requestApiJson("/api/purchase-guide", parsePurchaseGuide),
    requestApiJson("/api/inspection-guide", parseInspectionGuide),
    requestApiJson(
      `/api/vehicles?${catalogueQuery(catalogueDefaults, 1)}&pageSize=${CATALOGUE_PAGE_SIZE}`,
      parseCatalogue,
    ),
  ]);
  if (!purchase.data || !inspection.data) {
    const failed = purchase.data ? inspection : purchase;
    return (
      <StatusPage code={String(failed.status)} icon="alert-triangle" title="Warranty unavailable" />
    );
  }

  const faq = faqEntries(purchase.data, inspection.data.grading).filter(
    (entry) => entry.onWarrantyPage,
  );

  return (
    <ApexShieldGuide
      warranties={purchase.data.warranties}
      faq={faq}
      vehicles={(catalogue.data?.items ?? []).slice(0, CAROUSEL_CAR_COUNT)}
    />
  );
}

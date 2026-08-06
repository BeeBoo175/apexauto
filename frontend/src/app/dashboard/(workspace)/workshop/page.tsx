import { WorkshopPageEntry } from "@/components/inspection/WorkshopPageEntry";
import type { SearchParams } from "@/lib/query";

export default function InspectionPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <WorkshopPageEntry searchParams={searchParams} />;
}

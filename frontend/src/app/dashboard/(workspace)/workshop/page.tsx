import { WorkshopPageEntry } from "@/components/inspection/WorkshopPageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/workshop");

export default function InspectionPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <WorkshopPageEntry searchParams={searchParams} />;
}

import { notFound } from "next/navigation";
import { PerformanceClientEntry } from "@/components/performance/PerformanceClientEntry";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { hasPerformanceAccess } from "@/lib/performance/performance";
import { performanceSelection } from "@/lib/performance/performance-location";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/performance");

export default async function PerformancePage({
  params,
  searchParams,
}: {
  params: Promise<{ selection?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const selection = performanceSelection((await params).selection);
  if (!selection) {
    notFound();
  }
  return (
    <StaffEntry
      path="/dashboard/performance"
      searchParams={searchParams}
      access={hasPerformanceAccess}
    >
      {async (actor) => (
        <PerformanceClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={{ ...(await searchParams), person: "", section: "", ...selection }}
        />
      )}
    </StaffEntry>
  );
}

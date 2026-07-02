import { PerformanceClientEntry } from "@/components/performance/PerformanceClientEntry";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { hasPerformanceAccess } from "@/lib/performance/performance";
import type { SearchParams } from "@/lib/query";

export default async function PerformancePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/performance"
      searchParams={searchParams}
      access={hasPerformanceAccess}
      deniedTitle="Performance access unavailable"
      deniedDescription="Only branch managers and general managers can read performance."
    >
      {async (actor) => (
        <PerformanceClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={await searchParams}
        />
      )}
    </StaffEntry>
  );
}

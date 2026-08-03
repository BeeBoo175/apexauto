import { StaffTestDrivePageEntry } from "@/components/test-drives/StaffTestDrivePageEntry";
import type { SearchParams } from "@/lib/query";

export default function TestDrivesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <StaffTestDrivePageEntry searchParams={searchParams} />;
}

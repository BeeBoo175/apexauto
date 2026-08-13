import { StaffTestDrivePageEntry } from "@/components/test-drives/StaffTestDrivePageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/test-drives");

export default function TestDrivesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <StaffTestDrivePageEntry searchParams={searchParams} />;
}

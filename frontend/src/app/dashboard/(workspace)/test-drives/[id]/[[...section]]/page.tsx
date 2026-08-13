import { notFound } from "next/navigation";
import { StaffTestDrivePageEntry } from "@/components/test-drives/StaffTestDrivePageEntry";
import { isUuid } from "@/lib/auth/contracts";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/test-drives");

export default async function SelectedTestDrivePage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; section?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id, section = [] } = await params;
  if (!isUuid(id) || section.length > 1 || (section.length === 1 && section[0] !== "history")) {
    notFound();
  }
  return (
    <StaffTestDrivePageEntry selectedId={id} section={section[0]} searchParams={searchParams} />
  );
}

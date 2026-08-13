import { IntakePageEntry } from "@/components/staff/IntakePageEntry";
import { notFound } from "next/navigation";
import { isUuid } from "@/lib/auth/contracts";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/intake");

export default async function IntakeDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  return <IntakePageEntry selectedId={id} searchParams={searchParams} />;
}

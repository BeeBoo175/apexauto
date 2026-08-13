import { IntakePageEntry } from "@/components/staff/IntakePageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/intake");

export default async function IntakePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return <IntakePageEntry searchParams={searchParams} />;
}

import { IntakePageEntry } from "@/components/staff/IntakePageEntry";
import type { SearchParams } from "@/lib/query";

export default async function IntakePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return <IntakePageEntry searchParams={searchParams} />;
}

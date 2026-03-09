import { redirect } from "next/navigation";
import { legacyRepairsPath } from "@/lib/inspection/query";
import type { SearchParams } from "@/lib/query";

export default async function RepairsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  redirect(legacyRepairsPath(await searchParams));
}

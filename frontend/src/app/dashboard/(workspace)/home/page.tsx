import { HomeClientEntry } from "@/components/home/HomeClientEntry";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { hasHomeAccess, workspaceMetadata } from "@/lib/staff/navigation";
import type { SearchParams } from "@/lib/query";

export const metadata = workspaceMetadata("/dashboard/home");

export default async function HomePage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return (
    <StaffEntry path="/dashboard/home" searchParams={searchParams} access={hasHomeAccess}>
      {async (actor) => (
        <HomeClientEntry key={crypto.randomUUID()} actor={actor} parameters={await searchParams} />
      )}
    </StaffEntry>
  );
}

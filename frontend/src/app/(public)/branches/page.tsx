import type { Metadata } from "next";
import { ShowroomsWorkspace } from "@/components/guide/ShowroomsWorkspace";
import { requireShowrooms } from "@/lib/guide/guide-reads";
import { SHOWROOM_CLOSES_AT, SHOWROOM_OPENS_AT } from "@/lib/guide/visit-terms";

// The showrooms and their opening hours change only with a release, so the page is generated ahead of time and
// refreshed daily (PUBLIC_CONTENT_REVALIDATE_SECONDS). Each showroom's stock and cars are read in the browser.
export const revalidate = 86400;

export const metadata: Metadata = {
  title: "Our Showrooms — ApexAuto",
  description: `Open Monday to Saturday, ${SHOWROOM_OPENS_AT}–${SHOWROOM_CLOSES_AT} MYT.`,
};

export default async function BranchesPage() {
  const showrooms = await requireShowrooms();

  return <ShowroomsWorkspace showroomBranches={showrooms} />;
}

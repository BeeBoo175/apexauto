import type { Metadata } from "next";
import { ShowroomsWorkspace } from "@/components/guide/ShowroomsWorkspace";
import { StatusPage } from "@/components/ui/StatusPage";
import { readShowrooms } from "@/lib/guide/guide-reads";
import { SHOWROOM_CLOSES_AT, SHOWROOM_OPENS_AT } from "@/lib/guide/visit-terms";

// The showrooms and their opening hours change only with a release, so the page is generated ahead of time and
// refreshed hourly (PUBLIC_CONTENT_REVALIDATE_SECONDS). Each showroom's stock and cars are read in the browser.
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "Our Showrooms — ApexAuto",
  description: `Open Monday to Saturday, ${SHOWROOM_OPENS_AT}–${SHOWROOM_CLOSES_AT} MYT.`,
};

export default async function BranchesPage() {
  const result = await readShowrooms();
  if (!result.data) {
    return (
      <StatusPage
        code={String(result.status)}
        icon="alert-triangle"
        title="Showrooms unavailable"
      />
    );
  }

  return <ShowroomsWorkspace showroomBranches={result.showrooms} />;
}

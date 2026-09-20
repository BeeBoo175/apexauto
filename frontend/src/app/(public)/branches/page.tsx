import type { Metadata } from "next";
import { ShowroomsWorkspace } from "@/components/guide/ShowroomsDirectory";
import { StatusPage } from "@/components/ui/StatusPage";
import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import { resolveShowrooms } from "@/lib/guide/showrooms";
import { requestApiJson } from "@/lib/server/http/api";

// The showrooms and their opening hours change only with a release, so the page is generated ahead of time and
// refreshed on this interval. Each showroom's stock and cars are read in the browser.
const REVALIDATE_SECONDS = 3600;
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "Our Showrooms — ApexAuto",
  description: "Open Monday to Saturday, 09:00–18:00 MYT.",
};

export default async function BranchesPage() {
  const result = await requestApiJson(
    "/api/branches?page=1&pageSize=25",
    (value) => parsePage(value, parseBranch),
    { revalidateSeconds: REVALIDATE_SECONDS },
  );
  if (!result.data) {
    return (
      <StatusPage
        code={String(result.status)}
        icon="alert-triangle"
        title="Showrooms unavailable"
      />
    );
  }

  return <ShowroomsWorkspace showroomBranches={resolveShowrooms(result.data.items)} />;
}

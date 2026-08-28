import type { Metadata } from "next";
import { ShowroomsWorkspace } from "@/components/guide/ShowroomsDirectory";
import { StatusPage } from "@/components/ui/StatusPage";
import { parsePage } from "@/lib/api";
import { parseBranch } from "@/lib/branches";
import { resolveShowrooms } from "@/lib/guide/showrooms";
import { requestApiJson } from "@/lib/server/http/api";

export const metadata: Metadata = {
  title: "Our Showrooms — ApexAuto",
  description: "Open Monday to Saturday, 09:00–18:00 MYT.",
};

export default async function BranchesPage() {
  const result = await requestApiJson("/api/branches?page=1&pageSize=25", (value) =>
    parsePage(value, parseBranch),
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

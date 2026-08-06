import { notFound } from "next/navigation";
import { WorkshopPageEntry } from "@/components/inspection/WorkshopPageEntry";
import { workshopSelectionRoute } from "@/lib/inspection/workshop-location";
import type { SearchParams } from "@/lib/query";

export default async function SelectedInspectionPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; section?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id, section } = await params;
  const selection = workshopSelectionRoute(id, section);
  if (!selection) {
    notFound();
  }
  return <WorkshopPageEntry selection={selection} searchParams={searchParams} />;
}

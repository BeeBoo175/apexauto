import { notFound } from "next/navigation";
import { SettingsPageEntry } from "@/components/settings/SettingsPageEntry";
import { settingsSelectionRoute } from "@/lib/settings/settings-location";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/settings");

export default async function SelectedSettingsPage({
  params,
  searchParams,
}: {
  params: Promise<{ section: string; id?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const { section, id } = await params;
  const selection = settingsSelectionRoute(section, id);
  if (!selection) {
    notFound();
  }
  return <SettingsPageEntry searchParams={searchParams} selection={selection} />;
}

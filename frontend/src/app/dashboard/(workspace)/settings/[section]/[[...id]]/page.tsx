import { notFound } from "next/navigation";
import { SettingsPageEntry } from "@/components/settings/SettingsPageEntry";
import { settingsSelectionRoute } from "@/lib/settings/settings-location";
import type { SearchParams } from "@/lib/query";

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

import { SettingsPageEntry } from "@/components/settings/SettingsPageEntry";
import type { SearchParams } from "@/lib/query";

export default function SettingsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <SettingsPageEntry searchParams={searchParams} />;
}

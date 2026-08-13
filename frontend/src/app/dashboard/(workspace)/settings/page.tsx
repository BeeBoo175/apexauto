import { SettingsPageEntry } from "@/components/settings/SettingsPageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/settings");

export default function SettingsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <SettingsPageEntry searchParams={searchParams} />;
}

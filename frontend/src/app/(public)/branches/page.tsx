import type { Metadata } from "next";
import { ShowroomsWorkspace } from "@/components/guide/ShowroomsDirectory";

export const metadata: Metadata = {
  title: "Our Showrooms — ApexAuto",
  description: "Open Monday to Saturday, 09:00–18:00 MYT.",
};

export default function BranchesPage() {
  return <ShowroomsWorkspace />;
}

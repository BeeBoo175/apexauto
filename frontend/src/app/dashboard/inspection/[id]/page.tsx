import { notFound } from "next/navigation";
import { InspectionEntry } from "@/components/inspection/InspectionEntry";
import { readEditor } from "@/lib/inspection/editor-server";
import { isUuid } from "@/lib/auth/contracts";
import { InspectionEditor } from "@/components/inspection/InspectionEditor";
export default async function InspectionDetail({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  return (
    <InspectionEntry path={`/dashboard/inspection/${id}`}>
      {async (actor) => {
        const initial = await readEditor(id);
        return <InspectionEditor id={id} actor={actor} initial={initial} technicians={null} />;
      }}
    </InspectionEntry>
  );
}

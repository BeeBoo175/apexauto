import { notFound, redirect } from "next/navigation";
import { isUuid } from "@/lib/auth/contracts";

export default async function EditorPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  redirect(`/dashboard/inspection?inspection=${id}`);
}

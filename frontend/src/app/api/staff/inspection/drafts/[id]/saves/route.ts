import { relayInspectionResource } from "@/lib/inspection/routes";
import { parseSave } from "@/lib/inspection/parse";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayInspectionResource(request, id, `drafts/${id}/saves`, parseSave);
}

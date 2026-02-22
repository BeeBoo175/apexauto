import { relayInspectionResource } from "@/lib/inspection/routes";
import { parseDraft } from "@/lib/inspection/parse";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayInspectionResource(request, id, `drafts/${id}`, parseDraft);
}

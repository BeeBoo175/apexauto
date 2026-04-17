import { relayInspectionResource } from "@/lib/inspection/routes";
import { parseReopening } from "@/lib/inspection/parse-report";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayInspectionResource(request, id, `drafts/${id}/reopenings`, parseReopening);
}

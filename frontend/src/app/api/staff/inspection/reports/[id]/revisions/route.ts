import { relayInspectionResource, inspectionQuery } from "@/lib/inspection/routes";
import { parseRevisions } from "@/lib/inspection/parse-report";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayInspectionResource(
    request,
    id,
    `reports/${id}/revisions?${inspectionQuery(request, true)}`,
    parseRevisions,
  );
}

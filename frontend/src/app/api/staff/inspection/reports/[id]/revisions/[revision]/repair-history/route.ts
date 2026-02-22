import { relayInspectionResource, inspectionQuery } from "@/lib/inspection/routes";
import { parseRepairs } from "@/lib/inspection/parse-report";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; revision: string }> },
) {
  const { id, revision } = await context.params;
  return relayInspectionResource(
    request,
    id,
    `reports/${id}/revisions/${revision}/repair-history?${inspectionQuery(request, true)}`,
    parseRepairs,
    revision,
  );
}

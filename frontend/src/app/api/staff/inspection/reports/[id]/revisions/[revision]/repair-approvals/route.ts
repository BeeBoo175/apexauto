import { relayInspectionResource } from "@/lib/inspection/routes";
import { parseApproval } from "@/lib/inspection/parse-report";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; revision: string }> },
) {
  const { id, revision } = await context.params;
  return relayInspectionResource(
    request,
    id,
    `reports/${id}/revisions/${revision}/repair-approvals`,
    parseApproval,
    revision,
  );
}

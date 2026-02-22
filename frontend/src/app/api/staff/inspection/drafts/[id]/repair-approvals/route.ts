import { relayInspectionResource } from "@/lib/inspection/routes";
import { parseApproval } from "@/lib/inspection/parse-report";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayInspectionResource(request, id, `drafts/${id}/repair-approvals`, parseApproval);
}

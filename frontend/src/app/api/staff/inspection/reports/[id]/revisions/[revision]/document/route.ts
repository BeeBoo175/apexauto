import { inspectionReportDocument } from "@/lib/inspection/report-document-routes";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; revision: string }> },
) {
  const { id, revision } = await context.params;
  return inspectionReportDocument(request, id, revision);
}

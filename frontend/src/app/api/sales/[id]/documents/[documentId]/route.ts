import { relaySaleDocuments } from "@/lib/sales/document-routes";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; documentId: string }> },
) {
  const { id, documentId } = await context.params;
  return relaySaleDocuments(request, id, documentId);
}

import { relaySaleDocuments } from "@/lib/sales/document-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySaleDocuments(request, id);
}

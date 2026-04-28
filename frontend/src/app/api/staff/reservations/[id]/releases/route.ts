import { parseDeal } from "@/lib/deals/deals";
import { relaySalesWrite, shapeRelease } from "@/lib/sales/sales-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySalesWrite(request, `/reservations/${id}/releases`, shapeRelease, parseDeal);
}

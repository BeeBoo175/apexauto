import { parseDeal } from "@/lib/deals/deals";
import { relaySalesWrite, shapeAssignment } from "@/lib/sales/sales-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySalesWrite(request, `/reservations/${id}/assignments`, shapeAssignment, parseDeal);
}

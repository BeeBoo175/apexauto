import { parseHoldAgents } from "@/lib/sales/hold-agents";
import { relaySalesRead } from "@/lib/sales/sales-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySalesRead(request, `/reservations/${id}/agents`, parseHoldAgents);
}

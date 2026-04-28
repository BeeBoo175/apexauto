import { parseHoldHistory } from "@/lib/sales/history";
import { relaySalesRead } from "@/lib/sales/sales-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySalesRead(request, `/reservations/${id}/history`, parseHoldHistory, [
    "page",
    "pageSize",
  ]);
}

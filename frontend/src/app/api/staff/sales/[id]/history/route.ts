import { parseSaleHistory } from "@/lib/sales/sale-history";
import { relaySaleRead } from "@/lib/sales/sale-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySaleRead(request, `/${id}/history`, parseSaleHistory, ["page", "pageSize"]);
}

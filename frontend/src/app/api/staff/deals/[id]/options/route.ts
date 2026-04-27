import { parseDealOptions } from "@/lib/deals/deals";
import { relayDealRead } from "@/lib/deals/deal-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayDealRead(request, `/${id}/options`, parseDealOptions);
}

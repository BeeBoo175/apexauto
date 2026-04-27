import { parseDeal } from "@/lib/deals/deals";
import { relayDealRead, relayDealWrite, shapeDealRevise } from "@/lib/deals/deal-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayDealRead(request, `/${id}`, parseDeal);
}

export async function PUT(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayDealWrite(request, `/${id}`, "PUT", shapeDealRevise, parseDeal);
}

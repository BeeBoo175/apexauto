import { parseDeal, parseDealPage } from "@/lib/deals/deals";
import { relayDealList, relayDealWrite, shapeDealCreate } from "@/lib/deals/deal-routes";

export function GET(request: Request) {
  return relayDealList(request, parseDealPage);
}

export function POST(request: Request) {
  return relayDealWrite(request, "", "POST", shapeDealCreate, parseDeal);
}

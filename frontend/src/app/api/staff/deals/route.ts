import { parseDealPage } from "@/lib/deals/deals";
import { relayDealList } from "@/lib/deals/deal-routes";

export function GET(request: Request) {
  return relayDealList(request, parseDealPage);
}

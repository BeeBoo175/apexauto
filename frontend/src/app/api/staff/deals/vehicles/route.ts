import { parseEligibleVehicles } from "@/lib/deals/deals";
import { relayDealRead } from "@/lib/deals/deal-routes";

export function GET(request: Request) {
  return relayDealRead(request, "/vehicles", parseEligibleVehicles, ["search"]);
}

import { relayInspection, inspectionQuery } from "@/lib/inspection/routes";
import { parseWorkshopPage } from "@/lib/inspection/parse-workshop";

export function GET(request: Request) {
  return relayInspection(request, `workshop?${inspectionQuery(request)}`, parseWorkshopPage);
}

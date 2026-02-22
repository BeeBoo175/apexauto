import { relayInspection, inspectionQuery } from "@/lib/inspection/routes";
import { parseTechnicians } from "@/lib/inspection/parse";

export function GET(request: Request) {
  return relayInspection(
    request,
    `technicians?${inspectionQuery(request, true)}`,
    parseTechnicians,
  );
}

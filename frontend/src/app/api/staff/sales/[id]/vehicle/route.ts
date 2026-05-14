import { parseSaleVehicle } from "@/lib/sales/vehicle-reference";
import { relaySaleRead } from "@/lib/sales/routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relaySaleRead(request, `/${id}/vehicle`, parseSaleVehicle);
}

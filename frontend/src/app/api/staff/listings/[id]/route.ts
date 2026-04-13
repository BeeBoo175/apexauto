import { relayListingRead } from "@/lib/vehicles/listing-routes";
import { parseListingWorkspace } from "@/lib/vehicles/listing-workspace";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayListingRead(
    request,
    id,
    "",
    parseListingWorkspace,
    (value) => value.vehicle.vehicleId === id,
  );
}

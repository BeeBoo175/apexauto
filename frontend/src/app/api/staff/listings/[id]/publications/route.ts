import { relayListingCommand } from "@/lib/vehicles/listing-routes";
import { parseListingPublication } from "@/lib/vehicles/listing-publication";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayListingCommand(request, id, "publications", parseListingPublication);
}

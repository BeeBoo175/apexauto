import { relayListingCommand } from "@/lib/vehicles/listing-routes";
import { parseListingSave } from "@/lib/vehicles/listing-draft";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayListingCommand(request, id, "drafts", parseListingSave);
}

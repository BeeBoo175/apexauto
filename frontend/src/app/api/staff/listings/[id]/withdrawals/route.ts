import { relayListingCommand } from "@/lib/vehicles/listing-routes";
import { parseListingWithdrawal } from "@/lib/vehicles/listing-withdrawal";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayListingCommand(request, id, "withdrawals", parseListingWithdrawal);
}

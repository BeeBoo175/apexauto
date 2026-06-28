import { relayAccountBlockers } from "@/lib/accounts/account-administration-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayAccountBlockers(request, id);
}

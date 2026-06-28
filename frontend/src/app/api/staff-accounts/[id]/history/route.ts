import { relayAccountHistory } from "@/lib/accounts/staff-accounts-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayAccountHistory(request, id);
}

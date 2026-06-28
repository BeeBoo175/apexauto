import { relayAccountSuspension } from "@/lib/accounts/account-suspension-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayAccountSuspension(request, id, true);
}

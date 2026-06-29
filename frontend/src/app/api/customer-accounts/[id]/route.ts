import { relayCustomerAccount } from "@/lib/accounts/customer-accounts-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayCustomerAccount(request, id);
}

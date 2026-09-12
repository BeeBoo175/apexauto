import { relayCustomerErasurePreview } from "@/lib/accounts/account-erasure-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayCustomerErasurePreview(request, id);
}

import { relayCustomerSupport } from "@/lib/accounts/customer-support-routes";

export async function POST(
  request: Request,
  context: { params: Promise<{ id: string; action: string }> },
) {
  const { id, action } = await context.params;
  return relayCustomerSupport(request, id, action);
}

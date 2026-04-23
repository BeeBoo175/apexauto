import { relayCustomerCancellation } from "@/lib/vehicles/account-reservations-server";

export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  return relayCustomerCancellation(request, (await params).id);
}

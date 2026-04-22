import { relayCustomerReservation } from "@/lib/vehicles/customer-reservation-server";

export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return relayCustomerReservation(request, id);
}

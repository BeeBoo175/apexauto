import { relayTestDriveBooking } from "@/lib/test-drives/customer-server";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayTestDriveBooking(request, id);
}

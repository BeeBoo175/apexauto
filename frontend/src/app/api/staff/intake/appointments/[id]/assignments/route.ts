import { relayStaffAppointment } from "@/lib/intake/routes";
import { parseOwnershipResult } from "@/lib/staff/ownership";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  return relayStaffAppointment(request, context, "assignments", parseOwnershipResult);
}

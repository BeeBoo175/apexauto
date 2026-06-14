import { relayStaffAppointment } from "@/lib/intake/routes";
import { parseCheckIn } from "@/lib/staff/parse";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  return relayStaffAppointment(request, context, "no-show-corrections", parseCheckIn);
}

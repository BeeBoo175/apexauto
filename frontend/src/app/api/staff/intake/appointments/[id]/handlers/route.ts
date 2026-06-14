import { relayStaffAppointment } from "@/lib/intake/routes";
import { parseAppointmentHandlers } from "@/lib/staff/parse";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  return relayStaffAppointment(request, context, "handlers", parseAppointmentHandlers);
}

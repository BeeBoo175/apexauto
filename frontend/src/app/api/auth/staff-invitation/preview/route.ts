import {
  authFailure,
  authJson,
  authUpstream,
  readAuthRequest,
  upstreamDenial,
} from "@/lib/auth/http";
import { parseStaffInvitation } from "@/lib/auth/staff-invitation";

// Opening an invitation only reads it; nothing changes until the employee submits the form.
export async function POST(request: Request) {
  try {
    const body = await readAuthRequest(request, "staff-invitation-preview");
    const response = await authUpstream("/api/auth/staff-invitation/preview", {
      method: "POST",
      body,
      signal: request.signal,
    });

    if (response.status === 200) {
      const invitation = parseStaffInvitation(response.body);

      if (!invitation) {
        return authFailure(new Error("Unconfirmed invitation"));
      }

      return authJson({ ok: true, invitation });
    }

    if (response.status === 404) {
      return authJson({ message: "This invitation link is no longer available." }, 404);
    }

    return upstreamDenial(response.status, response.retryAfterSeconds);
  } catch (error) {
    return authFailure(error);
  }
}

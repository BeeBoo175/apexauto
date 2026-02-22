import { isUuid } from "@/lib/auth/contracts";
import { readCurrentActor } from "@/lib/auth/server";
import { intakeError, intakeJson } from "@/lib/intake/routes";
import { HttpError } from "@/lib/server/http/request";
import { readEditor } from "@/lib/inspection/editor-server";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  try {
    const { id } = await context.params;
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid inspection ID.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(20_000)]);
    const current = await readCurrentActor(signal);
    if (!current.actor) {
      throw new HttpError(
        current.status === 401 ? 401 : 502,
        "Your session could not be confirmed.",
      );
    }
    return intakeJson({ ...(await readEditor(id, signal)), actor: current.actor });
  } catch (error) {
    return intakeError(error);
  }
}

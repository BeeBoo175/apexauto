import { isRecord, isUuid } from "@/lib/auth/contracts";

// The staff name and optional picture a read attaches beside a handler's text identity.
export interface HandlerDisplay {
  name: string | null;
  pictureId: string | null;
}

export function parseHandlerDisplay(
  value: unknown,
  nameKey: string,
  pictureKey: string,
): HandlerDisplay | null {
  if (!isRecord(value)) {
    return null;
  }
  const name = value[nameKey] ?? null;
  const pictureId = value[pictureKey] ?? null;
  if (
    (name !== null && !(typeof name === "string" && !!name.trim())) ||
    (pictureId !== null && !isUuid(pictureId))
  ) {
    return null;
  }
  return { name, pictureId };
}

// The recorded name of a handler for a choice list; the email stands in only when no profile name was recorded.
export function handlerName(name: unknown, email: string) {
  return typeof name === "string" && name.trim() ? name : email;
}

import "server-only";
import { privateApiRead } from "@/lib/server/http/api";

export function inspectionRead<T>(
  path: string,
  parse: (v: unknown) => T | null,
  signal?: AbortSignal,
) {
  return privateApiRead(`/api/staff/inspection/${path}`, parse, signal, 1024 * 1024);
}

import "server-only";
import { privateIntakeRead } from "@/lib/intake/server";

export function inspectionRead<T>(
  path: string,
  parse: (v: unknown) => T | null,
  signal?: AbortSignal,
) {
  return privateIntakeRead(`/api/staff/inspection/${path}`, parse, signal, 1024 * 1024);
}

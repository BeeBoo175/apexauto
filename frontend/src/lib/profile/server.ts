import "server-only";
import { privateApiRead } from "@/lib/server/http/api";
import { parseAccountProfile } from "./contracts";

export function readOwnProfile(signal?: AbortSignal) {
  return privateApiRead("/api/account/profile", parseAccountProfile, signal);
}

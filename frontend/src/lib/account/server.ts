import "server-only";
import { privateApiRead } from "@/lib/server/http/api";
import { parseAccountProfile, parsePerson } from "./contracts";

export function readOwnProfile(signal?: AbortSignal) {
  return privateApiRead("/api/account/profile", parseAccountProfile, signal);
}

export function readPerson(id: string, signal?: AbortSignal) {
  return privateApiRead(`/api/people/${id}`, parsePerson, signal);
}

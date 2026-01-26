import "server-only";

import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { readOwnAccount } from "@/lib/auth/server";
import { SessionCheckUnavailable } from "./SessionCheckUnavailable";

export async function GuestOnly({ children }: { children: ReactNode }) {
  let response: Awaited<ReturnType<typeof readOwnAccount>>;

  try {
    response = await readOwnAccount();
  } catch {
    return <SessionCheckUnavailable />;
  }

  if (response.status === 200 && response.account) {
    redirect("/account");
  }

  // The backend authenticates the session before denying a non-customer account read.
  if (response.status === 403) {
    redirect("/");
  }

  if (response.status !== 401) {
    return <SessionCheckUnavailable />;
  }

  return children;
}

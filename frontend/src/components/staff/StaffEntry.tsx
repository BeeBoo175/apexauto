import "server-only";
import type { ReactNode } from "react";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { forbidden } from "next/navigation";
import { readCurrentActor } from "@/lib/auth/server";
import { staffActor } from "@/lib/staff/server";
import { StaffActorBoundary } from "./StaffActorBoundary";
import { StaffLink } from "./StaffLink";
import { StaffNotice } from "./StaffPage";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";

export async function StaffEntry({
  path,
  access,
  searchParams,
  allowUnverified = false,
  children,
}: {
  path: string;
  access: (actor: CurrentActor) => boolean;
  searchParams?: Promise<SearchParams>;
  // The account page stays open to staff who still have to verify their email.
  allowUnverified?: boolean;
  children: (actor: CurrentActor) => Promise<ReactNode>;
}) {
  const { actor, status } = await staffActor(path);
  if (!actor) {
    return (
      <>
        <LoadingProgressReady />
        <SessionCheckUnavailable status={status} />
      </>
    );
  }

  // Staff with an unconfirmed email may only verify; the backend denies their other reads.
  const { emailVerified } = await readCurrentActor();
  const unverified = !emailVerified && !allowUnverified;

  // Run page reads only after the current request's actor and feature access are confirmed.
  const permitted = access(actor) && !unverified;
  const parameters = permitted && searchParams ? await searchParams : {};
  let content: ReactNode;
  if (unverified) {
    content = (
      <>
        <LoadingProgressReady />
        <StaffNotice title="Verify your email">
          <p>
            Confirm your email address to use the staff workspace. Request a new link from your
            account if the first has expired.
          </p>
          <StaffLink href="/dashboard/profile">Open your account</StaffLink>
        </StaffNotice>
      </>
    );
  } else if (permitted) {
    content = await children(actor);
  } else {
    forbidden();
  }

  return (
    <StaffActorBoundary
      actor={actor}
      path={path}
      parameters={parameters}
      snapshotId={crypto.randomUUID()}
    >
      {content}
    </StaffActorBoundary>
  );
}

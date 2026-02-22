import type { ReactNode } from "react";
import { StaffActorBoundary } from "@/components/staff/StaffActorBoundary";
import { StaffNotice } from "@/components/staff/StaffPage";
import { StaffLink } from "@/components/staff/StaffLink";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { staffActor } from "@/lib/staff/server";
import type { CurrentActor } from "@/lib/auth/actor";
import { hasInspectionAccess } from "@/lib/inspection/contracts";

export async function InspectionEntry({
  path,
  children,
}: {
  path: string;
  children: (actor: CurrentActor) => Promise<ReactNode>;
}) {
  const actor = await staffActor(path);
  if (!actor) {
    return <SessionCheckUnavailable />;
  }
  const content = hasInspectionAccess(actor) ? (
    await children(actor)
  ) : (
    <StaffNotice title="Inspection access unavailable">
      <p>This role cannot access inspections or repair requests.</p>
      <StaffLink href="/dashboard">Return to your staff workspace</StaffLink>
    </StaffNotice>
  );
  return (
    <StaffActorBoundary actor={actor} path={path} snapshotId={crypto.randomUUID()}>
      {content}
    </StaffActorBoundary>
  );
}

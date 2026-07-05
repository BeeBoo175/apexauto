"use client";

import { useContext, type ReactNode, type Ref } from "react";
import { Card } from "@/components/ui/Card";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext } from "./StaffActorBoundary";
import { StaffEntryPresentationContext } from "./StaffClientEntryBoundary";
import { StaffLoadingShell, type StaffLoadingView } from "./StaffLoadingShell";
import { staffPageTitles } from "@/lib/staff/navigation";
import styles from "./Staff.module.css";
import loading from "./StaffLoadingShell.module.css";

export function StaffLoading() {
  const presentation = useContext(StaffEntryPresentationContext);

  if (!presentation) {
    throw new Error("Staff loading requires its authorized page presentation");
  }

  const { actor, path: pathname, parameters } = presentation;
  const workshopView: StaffLoadingView =
    actor.role === "WorkshopTechnician" ? "technician" : "inspection";
  const views: Record<string, StaffLoadingView> = {
    "/dashboard/intake": "intake",
    "/dashboard/inspection": workshopView,
    "/dashboard/inspection/repairs": "repairs",
    "/dashboard/settings": "inspection",
    // Agents get the queue workspace and managers the Sales table, so the shell follows the role.
    "/dashboard/sales": actor.role === "SalesAgent" ? "technician" : "inspection",
    "/dashboard/test-drives": actor.role === "SalesAgent" ? "technician" : "inspection",
    "/dashboard/work-calendar": "calendar",
    "/dashboard/account": "appointment",
    "/dashboard/accounts": "intake",
    "/dashboard/performance": "report",
  };
  let view = views[pathname] ?? "inspection";
  let title = staffPageTitles[pathname] ?? "Your staff workspace";
  if (pathname === "/dashboard/intake") {
    view = actor.role === "SalesAgent" ? "technician" : "intake";
  }
  if (pathname.startsWith("/dashboard/intake/")) {
    view = "appointment";
    title = "Intake appointment";
  }
  return (
    <>
      <LoadingProgress label="Opening workspace" />
      <StaffCommandBar title={title} />
      <StaffLoadingShell
        view={view}
        selected={Boolean(
          parameters.inspection || parameters.sale || parameters.appointment || parameters.booking,
        )}
        report={{
          view: typeof parameters.view === "string" ? parameters.view : "",
          general: actor.role === "GeneralManager",
          agent: actor.role === "SalesAgent",
        }}
      />
    </>
  );
}

export function StaffCommandBar({
  title,
  actions,
  actionsRef,
}: {
  title: string | null;
  actions?: ReactNode;
  actionsRef?: Ref<HTMLDivElement>;
}) {
  const navigation = useContext(StaffNavigationContext);
  const pendingTitle = navigation?.pendingPath
    ? staffPageTitles[navigation.pendingPath]
    : undefined;
  return (
    <header className={styles.commandBar}>
      {title === null ? (
        <span className={loading.commandTitle} aria-hidden="true" />
      ) : (
        <h1 tabIndex={-1}>{pendingTitle ?? title}</h1>
      )}
      {(actions || actionsRef) && (
        <div ref={actionsRef} className={styles.actions}>
          {actions}
        </div>
      )}
    </header>
  );
}

export function StaffNotice({ title, children }: { title: string; children: ReactNode }) {
  return (
    <>
      <StaffCommandBar title={title} />
      <Card className={styles.notice}>{children}</Card>
    </>
  );
}

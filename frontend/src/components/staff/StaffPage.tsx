"use client";

import { useContext, type ReactNode } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { Card } from "@/components/ui/Card";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext } from "./StaffActorBoundary";
import { StaffLoadingShell, type StaffLoadingView } from "./StaffLoadingShell";
import reportStyles from "@/components/inspection/SignedReport.module.css";
import styles from "./Staff.module.css";

const titles: Record<string, string> = {
  "/dashboard/intake": "Intake appointments",
  "/dashboard/inspection": "Inspections",
  "/dashboard/inspection/repairs": "Repair requests",
};

export function StaffLoading() {
  const pathname = usePathname();
  const search = useSearchParams();
  const views: Record<string, StaffLoadingView> = {
    "/dashboard/intake": search.get("view") === "list" ? "intake" : "calendar",
    "/dashboard/inspection": "inspection",
    "/dashboard/inspection/repairs": "repairs",
  };
  let view = views[pathname] ?? "inspection";
  let title = titles[pathname] ?? "Your staff workspace";
  if (pathname.startsWith("/dashboard/intake/")) {
    view = "appointment";
    title = "Intake appointment";
  }
  if (pathname.includes("/reports/")) {
    view = "report";
    title = "Signed report";
  }
  return (
    <>
      <LoadingProgress label="Opening workspace" />
      <div className={view === "report" ? reportStyles.controls : undefined}>
        <StaffCommandBar title={title} />
      </div>
      <StaffLoadingShell view={view} />
    </>
  );
}

export function StaffCommandBar({ title, actions }: { title: string; actions?: ReactNode }) {
  const navigation = useContext(StaffNavigationContext);
  const pendingTitle = navigation?.pendingPath ? titles[navigation.pendingPath] : undefined;
  return (
    <header className={styles.commandBar}>
      <h1 tabIndex={-1}>{pendingTitle ?? title}</h1>
      {actions && <div className={styles.actions}>{actions}</div>}
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

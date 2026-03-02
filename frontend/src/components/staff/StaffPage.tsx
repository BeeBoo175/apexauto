"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";
import { Card } from "@/components/ui/Card";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import styles from "./Staff.module.css";

export function StaffLoading() {
  const pathname = usePathname();
  const titles: Record<string, string> = {
    "/dashboard/intake": "Intake appointments",
    "/dashboard/inspection": "Inspections",
    "/dashboard/inspection/repairs": "Repair requests",
  };
  return (
    <>
      <LoadingProgress label="Opening workspace" />
      <StaffCommandBar title={titles[pathname] ?? "Your staff workspace"} />
    </>
  );
}

export function StaffCommandBar({ title, actions }: { title: string; actions?: ReactNode }) {
  return (
    <header className={styles.commandBar}>
      <h1 tabIndex={-1}>{title}</h1>
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

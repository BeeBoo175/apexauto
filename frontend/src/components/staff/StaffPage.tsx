import type { ReactNode } from "react";
import { Card } from "@/components/ui/Card";
import styles from "./Staff.module.css";

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

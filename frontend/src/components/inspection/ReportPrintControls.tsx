"use client";
import { Button } from "@/components/ui/Button";
import { useInspectionPrint } from "@/hooks/use-inspection-print";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import styles from "./SignedReport.module.css";

export function ReportPrintControls() {
  const preparation = useInspectionPrint();
  return (
    <>
      <StaffCommandBar
        title="Signed report"
        actions={
          <Button
            size="compact"
            disabled={preparation.busy}
            onClick={() => void preparation.print()}
          >
            {preparation.busy ? `Preparing ${preparation.progress}` : "Print / save PDF"}
          </Button>
        }
      />
      {preparation.message && (
        <p className={styles.printMessage} role="alert">
          {preparation.message}
        </p>
      )}
    </>
  );
}

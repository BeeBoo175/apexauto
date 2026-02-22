"use client";
import { Button } from "@/components/ui/Button";
import { useInspectionPrint } from "@/hooks/use-inspection-print";
import { StaffCommandBar } from "@/components/staff/StaffPage";

export function ReportPrintControls() {
  const preparation = useInspectionPrint();
  return (
    <>
      <StaffCommandBar
        title="Signed report"
        actions={
          <Button
            size="compact"
            variant="shell"
            disabled={preparation.busy}
            onClick={() => void preparation.print()}
          >
            {preparation.busy ? `Preparing ${preparation.progress}` : "Print / save PDF"}
          </Button>
        }
      />
      {preparation.message && (
        <p className="px-5 py-3 text-sm" role="alert">
          {preparation.message}
        </p>
      )}
    </>
  );
}

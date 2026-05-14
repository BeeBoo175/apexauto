"use client";

import { useState } from "react";
import { StaffDetailsDialog } from "@/components/staff/StaffDetailsDialog";
import { Button } from "@/components/ui/Button";
import type { RepairRequest, Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { label } from "@/lib/inspection/contracts";
import { SignedFinding } from "./SignedFinding";
import { InspectionPointStatus } from "./WorkshopStatus";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import workshop from "./Workshop.module.css";
import record from "@/components/staff/StaffRecordContent.module.css";

export function RepairRequestDetails({
  request,
  report,
}: {
  request: RepairRequest;
  report: Report;
}) {
  const [selectedCode, setSelectedCode] = useState(request.items[0]?.pointCode ?? "");
  const selectedIndex = Math.max(
    0,
    request.items.findIndex((item) => item.pointCode === selectedCode),
  );
  const selected = request.items[selectedIndex];
  const finding = report.checklist.find((point) => point.code === selected?.pointCode);
  const multipleFindings = request.items.length > 1;
  return (
    <div className={workbench.stack}>
      <div className={workshop.split} data-single-item={!multipleFindings}>
        {multipleFindings && (
          <nav className={workshop.index} aria-label="Requested work">
            {request.items.map((item) => {
              const itemFinding = report.checklist.find((point) => point.code === item.pointCode);
              return (
                <Button
                  key={item.pointCode}
                  size="compact"
                  variant="ghost"
                  className={workshop.indexItem}
                  aria-pressed={selected?.pointCode === item.pointCode}
                  onClick={() => setSelectedCode(item.pointCode)}
                >
                  <span className={workshop.pointCode}>{item.pointCode}</span>
                  <strong>{itemFinding?.description}</strong>
                  <InspectionPointStatus kind="Finding" severity={itemFinding?.severity} compact />
                  <span>{formatCurrency(itemFinding?.estimatedRepairCost ?? 0)}</span>
                </Button>
              );
            })}
          </nav>
        )}
        {selected && (
          <section className={workshop.detail} aria-label="Selected repair work">
            <div className={record.heading}>
              <div>
                <span className={record.context}>
                  <span className={workshop.pointCode}>{selected.pointCode}</span> ·{" "}
                  {finding ? label(finding.category) : "Repair work"}
                </span>
                <h3 className={record.title}>{finding?.description ?? selected.pointCode}</h3>
                {finding?.severity && (
                  <div>
                    <InspectionPointStatus kind="Finding" severity={finding.severity} />
                  </div>
                )}
              </div>
            </div>
            <div className={record.group}>
              <h4>Requested work</h4>
              <p>{selected.work}</p>
            </div>
            {finding && (
              <SignedFinding
                point={finding}
                inspectionId={report.inspectionId}
                revision={report.revision}
              />
            )}
            {multipleFindings && (
              <div className={workshop.detailPagination}>
                <span>
                  {selectedIndex + 1} of {request.items.length} findings
                </span>
                <Button
                  size="compact"
                  variant="secondary"
                  onClick={() =>
                    setSelectedCode(
                      request.items[(selectedIndex + 1) % request.items.length].pointCode,
                    )
                  }
                >
                  Next finding
                </Button>
              </div>
            )}
          </section>
        )}
      </div>
    </div>
  );
}

export function RepairRequestMetadata({ request }: { request: RepairRequest }) {
  return (
    <StaffDetailsDialog
      title="Request details"
      details={[
        { label: "Requested budget", value: formatCurrency(request.budget) },
        { label: "Requested", value: formatMalaysianDateTime(request.requestedAt) },
        { label: "Signed revision", value: request.revision },
        { label: "Work version", value: request.version },
      ]}
    />
  );
}

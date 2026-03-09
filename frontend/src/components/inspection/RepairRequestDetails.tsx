"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import type { RepairRequest, Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { label } from "@/lib/inspection/contracts";
import { SignedFinding } from "./SignedFinding";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import workshop from "./Workshop.module.css";

export function RepairRequestDetails({
  request,
  report,
  showMetadata = true,
}: {
  request: RepairRequest;
  report: Report;
  showMetadata?: boolean;
}) {
  const [selectedCode, setSelectedCode] = useState(request.items[0]?.pointCode ?? "");
  const selected =
    request.items.find((item) => item.pointCode === selectedCode) ?? request.items[0];
  const finding = report.checklist.find((point) => point.code === selected?.pointCode);
  return (
    <div className={workbench.stack}>
      {showMetadata && (
        <details className={workshop.metadata}>
          <summary>Request details</summary>
          <dl className={workshop.facts}>
            <div>
              <dt>Requested budget</dt>
              <dd>{formatCurrency(request.budget)}</dd>
            </div>
            <div>
              <dt>Requested</dt>
              <dd>{formatMalaysianDateTime(request.requestedAt)}</dd>
            </div>
            <div>
              <dt>Signed revision</dt>
              <dd>{request.revision}</dd>
            </div>
            <div>
              <dt>Work version</dt>
              <dd>{request.version}</dd>
            </div>
          </dl>
        </details>
      )}
      <div className={workshop.split}>
        <nav className={workshop.index} aria-label="Requested work">
          {request.items.map((item) => (
            <Button
              key={item.pointCode}
              size="compact"
              variant="ghost"
              className={workshop.indexItem}
              aria-pressed={selected?.pointCode === item.pointCode}
              onClick={() => setSelectedCode(item.pointCode)}
            >
              <span className={workshop.pointCode}>{item.pointCode}</span>
              <strong>
                {report.checklist.find((point) => point.code === item.pointCode)?.description}
              </strong>
              <span className={workshop.pointState}>
                {label(
                  report.checklist.find((point) => point.code === item.pointCode)?.severity ??
                    "Finding",
                )}
              </span>
              <span>
                {formatCurrency(
                  report.checklist.find((point) => point.code === item.pointCode)
                    ?.estimatedRepairCost ?? 0,
                )}
              </span>
            </Button>
          ))}
        </nav>
        {selected && (
          <section className={workshop.detail} aria-label="Selected repair work">
            <div className={workshop.detailHeading}>
              <span className={workshop.pointCode}>
                {selected.pointCode} · {finding ? label(finding.category) : "Repair work"}
              </span>
              <h3>{finding?.description ?? selected.pointCode}</h3>
            </div>
            <div className={workshop.detailSection}>
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
            <div className={workshop.detailPagination}>
              <span>
                {request.items.indexOf(selected) + 1} of {request.items.length} findings
              </span>
              <Button
                size="compact"
                variant="secondary"
                onClick={() =>
                  setSelectedCode(
                    request.items[(request.items.indexOf(selected) + 1) % request.items.length]
                      .pointCode,
                  )
                }
              >
                Next finding
              </Button>
            </div>
          </section>
        )}
      </div>
    </div>
  );
}

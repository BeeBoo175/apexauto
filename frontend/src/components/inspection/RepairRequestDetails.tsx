import type { RepairRequest, Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { label } from "@/lib/inspection/contracts";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/AppointmentWorkbench.module.css";

export function RepairRequestDetails({
  request,
  report,
}: {
  request: RepairRequest;
  report: Report;
}) {
  return (
    <div className={workbench.stack}>
      <dl className={workbench.details}>
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
      {request.items.map((item) => {
        const finding = report.checklist.find((point) => point.code === item.pointCode);
        return (
          <section className={workbench.protocol} key={item.pointCode}>
            <h3>
              {item.pointCode} · {finding?.description}
            </h3>
            <p>{item.work}</p>
            {finding && (
              <>
                <p className={workbench.help}>
                  {finding.severity ? label(finding.severity) : label(finding.kind ?? "Passed")} ·
                  Original estimate {formatCurrency(finding.estimatedRepairCost)}
                </p>
                {finding.notes && <p>{finding.notes}</p>}
                <div className={styles.photos}>
                  {finding.photos.map((photo, index) => (
                    <figure key={photo.id}>
                      {/* Signed private evidence uses the existing authenticated media endpoint. */}
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img
                        src={`/api/staff/inspection/drafts/${report.inspectionId}/photos/${photo.id}`}
                        alt={`${item.pointCode} signed evidence ${index + 1}`}
                        loading="lazy"
                        width={photo.width}
                        height={photo.height}
                      />
                    </figure>
                  ))}
                </div>
              </>
            )}
          </section>
        );
      })}
    </div>
  );
}

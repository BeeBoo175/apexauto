import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate, formatMalaysianDateTime } from "@/lib/date-format";
import type { Deal } from "@/lib/deals/deals";
import workbench from "@/components/staff/StaffWorkspace.module.css";

type Fact = [label: string, value: React.ReactNode];

function FactList({ title, facts }: { title: string; facts: Fact[] }) {
  return (
    <section className={workbench.stack}>
      <h3>{title}</h3>
      <dl className={workbench.details}>
        {facts.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

function contactLink(scheme: "tel" | "mailto", value: string) {
  return value ? <a href={`${scheme}:${value}`}>{value}</a> : "Not on file";
}

// The people, vehicle and arrangements behind the price lines, from the server-resolved context.
export function DealFacts({ deal }: { deal: Deal }) {
  const { context } = deal;
  const submitted = deal.events.find((event) => event.kind === "Submitted");
  const dealFacts: Fact[] = [
    ["Sales agent", context.agent],
    ["Branch", context.branch],
    [
      "Submitted",
      submitted ? formatMalaysianDateTime(submitted.recordedAtUtc) : "Not yet submitted",
    ],
  ];
  if (deal.decidedById) {
    dealFacts.push(["Decided by", context.staff[deal.decidedById] ?? "Unknown"]);
  }
  if (context.holdCreatedAtUtc && context.holdExpiresAtUtc) {
    dealFacts.push([
      "Converted from hold",
      `Hold from ${formatMalaysianDate(context.holdCreatedAtUtc)} to ${formatMalaysianDate(context.holdExpiresAtUtc)}`,
    ]);
  }
  if (deal.warrantyName && context.warrantyMonths && context.warrantyMileageCapKm) {
    dealFacts.push([
      "Warranty",
      `${deal.warrantyName} · ${context.warrantyMonths} months or ${context.warrantyMileageCapKm.toLocaleString("en-MY")} km`,
    ]);
  }
  if (context.tradeIn && context.tradeInMinimum !== null && context.tradeInMaximum !== null) {
    dealFacts.push([
      "Trade-in",
      `${context.tradeIn} · valued ${formatCurrency(context.tradeInMinimum)}–${formatCurrency(context.tradeInMaximum)}`,
    ]);
  }

  return (
    <>
      <FactList
        title="Vehicle"
        facts={[
          ["Registration", context.registration],
          ["Vehicle", context.vehicle],
          ["VIN", context.vin],
          ["Mileage", `${context.mileageKilometres.toLocaleString("en-MY")} km`],
        ]}
      />
      <FactList
        title="Customer"
        facts={[
          ["Name", context.customer],
          ["Mobile", contactLink("tel", context.customerMobile)],
          ["Email", contactLink("mailto", context.customerEmail)],
        ]}
      />
      <FactList title="Deal" facts={dealFacts} />
    </>
  );
}

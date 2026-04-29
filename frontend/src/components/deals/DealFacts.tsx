import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate, formatMalaysianDateTime } from "@/lib/date-format";
import type { Deal } from "@/lib/deals/deals";
import { PersonIdentityFacts, VehicleIdentityFacts } from "@/components/staff/IdentityFacts";
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
      <section className={workbench.stack}>
        <h3>Vehicle</h3>
        <dl className={workbench.details}>
          <VehicleIdentityFacts
            registration={context.registration}
            vehicle={context.vehicle}
            vin={context.vin}
          />
          <div>
            <dt>Mileage</dt>
            <dd>{context.mileageKilometres.toLocaleString("en-MY")} km</dd>
          </div>
        </dl>
      </section>
      <section className={workbench.stack}>
        <h3>Customer</h3>
        <dl className={workbench.details}>
          <PersonIdentityFacts
            role="Name"
            name={context.customer}
            mobile={context.customerMobile}
            email={context.customerEmail}
          />
        </dl>
      </section>
      <FactList title="Deal" facts={dealFacts} />
    </>
  );
}

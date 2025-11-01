import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";

export default function Home() {
  return (
    <div className="max-w-6xl mx-auto px-6 py-12 flex flex-col gap-12">
      <section className="flex flex-col gap-4 border-b border-line pb-12">
        <div className="flex items-center gap-3">
          <Badge variant="trust">Malaysia Retail DMS</Badge>
          <span className="text-xs text-muted font-mono uppercase tracking-wider">
            Verified Single Record Lifecycle
          </span>
        </div>
        <h1 className="text-5xl md:text-7xl font-black uppercase font-display tracking-tight text-graphite leading-none">
          The Editorial <span className="text-signal">Service Bay</span>
        </h1>
        <p className="max-w-2xl text-muted text-base leading-relaxed">
          High-precision dealership operations connecting public customer intake directly to
          standardized 210-point inspections, storefront 48-hour reservations, and Deal Desk
          contract closing.
        </p>
        <div className="flex items-center gap-4 pt-4">
          <Button variant="primary" size="lg">
            Browse Verified Vehicles
          </Button>
          <Button variant="secondary" size="lg">
            Instant Trade-In Valuation
          </Button>
        </div>
      </section>

      <section className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <Card>
          <span className="text-xs font-bold text-signal font-mono uppercase">Stage 01</span>
          <h3 className="text-base font-bold uppercase tracking-wide mt-2">Intake &amp; Appraisal</h3>
          <p className="text-xs text-muted mt-2 leading-normal">
            Carsome-style 4-step wizard with pure C# algorithmic depreciation curves and S3 photo
            capture.
          </p>
        </Card>

        <Card>
          <span className="text-xs font-bold text-signal font-mono uppercase">Stage 02</span>
          <h3 className="text-base font-bold uppercase tracking-wide mt-2">Inspection &amp; Recond</h3>
          <p className="text-xs text-muted mt-2 leading-normal">
            Static 210 checklist catalog with defect exception logging and Puspakom/B5 grading.
          </p>
        </Card>

        <Card>
          <span className="text-xs font-bold text-signal font-mono uppercase">Stage 03</span>
          <h3 className="text-base font-bold uppercase tracking-wide mt-2">Storefront &amp; Hold</h3>
          <p className="text-xs text-muted mt-2 leading-normal">
            RSC vehicle catalogue with live trade-in loan offset and 48-hour row-locked reservation.
          </p>
        </Card>

        <Card>
          <span className="text-xs font-bold text-signal font-mono uppercase">Stage 04</span>
          <h3 className="text-base font-bold uppercase tracking-wide mt-2">Deal Desk &amp; Closing</h3>
          <p className="text-xs text-muted mt-2 leading-normal">
            Itemized VPO contract with discount gate, bank LOU settlement, and printable receipts.
          </p>
        </Card>
      </section>
    </div>
  );
}

"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select, Textarea } from "@/components/ui/Input";
import type { useListingEditor } from "@/hooks/use-listing-editor";
import { fuels, transmissions } from "@/lib/vehicles/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingDraftFields({ editor }: { editor: ReturnType<typeof useListingEditor> }) {
  const [editing, setEditing] = useState<"price" | "specifications" | "description" | null>(null);
  const { value, operating, validationAttempted, change } = editor;
  const price = parseMyr(value.vehiclePrice);
  const roadTax = parseMyr(value.roadTax);

  return (
    <div className={workbench.stack}>
      <section className={workbench.stack} aria-label="Listing price">
        <div className={workshop.detailHeading}>
          <h3>Vehicle price</h3>
          <Button
            size="compact"
            variant="ghost"
            disabled={operating}
            aria-expanded={editing === "price"}
            onClick={() => setEditing(editing === "price" ? null : "price")}
          >
            {editing === "price" ? "Done editing price" : "Edit price"}
          </Button>
        </div>
        {editing === "price" ? (
          <Input
            label="Vehicle price (MYR)"
            inputMode="decimal"
            density="compact"
            value={value.vehiclePrice}
            required
            aria-invalid={validationAttempted && price === null}
            disabled={operating}
            onChange={(event) => change({ ...value, vehiclePrice: event.target.value })}
          />
        ) : (
          <p>{price === null ? value.vehiclePrice || "Not set" : formatCurrency(price)}</p>
        )}
      </section>
      <section className={workbench.stack} aria-label="Listing specifications">
        <div className={workshop.detailHeading}>
          <h3>Specifications</h3>
          <Button
            size="compact"
            variant="ghost"
            disabled={operating}
            aria-expanded={editing === "specifications"}
            onClick={() => setEditing(editing === "specifications" ? null : "specifications")}
          >
            {editing === "specifications" ? "Done editing specifications" : "Edit specifications"}
          </Button>
        </div>
        {editing === "specifications" ? (
          <div className={workbench.stack}>
            <Select
              label="Transmission"
              density="compact"
              value={value.transmission}
              required
              aria-invalid={
                validationAttempted && !transmissions.some((name) => name === value.transmission)
              }
              disabled={operating}
              onChange={(event) => change({ ...value, transmission: event.target.value })}
            >
              <option value="">Choose transmission</option>
              {transmissions.map((name) => (
                <option key={name} value={name}>
                  {name}
                </option>
              ))}
            </Select>
            <Select
              label="Fuel"
              density="compact"
              value={value.fuel}
              required
              aria-invalid={validationAttempted && !fuels.some((name) => name === value.fuel)}
              disabled={operating}
              onChange={(event) => change({ ...value, fuel: event.target.value })}
            >
              <option value="">Choose fuel</option>
              {fuels.map((name) => (
                <option key={name} value={name}>
                  {name === "PlugInHybrid" ? "Plug-in hybrid" : name}
                </option>
              ))}
            </Select>
            <Input
              label="Road tax (MYR)"
              density="compact"
              inputMode="decimal"
              value={value.roadTax}
              required
              aria-invalid={validationAttempted && roadTax === null}
              disabled={operating}
              onChange={(event) => change({ ...value, roadTax: event.target.value })}
            />
          </div>
        ) : (
          <dl className={workshop.facts}>
            <div>
              <dt>Transmission</dt>
              <dd>{value.transmission || "Not set"}</dd>
            </div>
            <div>
              <dt>Fuel</dt>
              <dd>{value.fuel === "PlugInHybrid" ? "Plug-in hybrid" : value.fuel || "Not set"}</dd>
            </div>
            <div>
              <dt>Road tax (MYR)</dt>
              <dd>{roadTax === null ? value.roadTax || "Not set" : formatCurrency(roadTax)}</dd>
            </div>
          </dl>
        )}
      </section>
      <section className={workbench.stack} aria-label="Listing description">
        <div className={workshop.detailHeading}>
          <h3>Description</h3>
          <Button
            size="compact"
            variant="ghost"
            disabled={operating}
            aria-expanded={editing === "description"}
            onClick={() => setEditing(editing === "description" ? null : "description")}
          >
            {editing === "description" ? "Done editing description" : "Edit description"}
          </Button>
        </div>
        {editing === "description" ? (
          <Textarea
            label="Public description"
            rows={5}
            maxLength={2000}
            value={value.description}
            required
            aria-invalid={
              validationAttempted &&
              (!value.description.trim() ||
                value.description.trim().length > 2000 ||
                value.description.includes("\0"))
            }
            disabled={operating}
            onChange={(event) => change({ ...value, description: event.target.value })}
          />
        ) : (
          <p>{value.description || "No listing description has been saved."}</p>
        )}
      </section>
    </div>
  );
}

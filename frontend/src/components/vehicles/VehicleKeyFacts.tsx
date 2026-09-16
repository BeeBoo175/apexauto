import styles from "./VehicleKeyFacts.module.css";

interface VehicleKeyFactsProps {
  year: number;
  mileage: string;
  transmission: string;
  fuel: string;
}

// The four facts a buyer checks first, repeated under the photographs.
export function VehicleKeyFacts({ year, mileage, transmission, fuel }: VehicleKeyFactsProps) {
  return (
    <dl className={styles.facts} aria-label="Key facts">
      <div>
        <dt>Year</dt>
        <dd>{year}</dd>
      </div>
      <div>
        <dt>Mileage</dt>
        <dd>{mileage}</dd>
      </div>
      <div>
        <dt>Transmission</dt>
        <dd>{transmission}</dd>
      </div>
      <div>
        <dt>Fuel</dt>
        <dd>{fuel}</dd>
      </div>
    </dl>
  );
}

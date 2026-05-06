import styles from "./IdentityFacts.module.css";

// Rows for a drawer's fact list: the one way staff drawers present a vehicle and a person.
export function VehicleIdentityFacts({
  registration,
  vehicle,
  vin,
}: {
  registration: string;
  vehicle: string;
  vin?: string;
}) {
  return (
    <>
      <div>
        <dt>Registration</dt>
        <dd>{registration}</dd>
      </div>
      <div>
        <dt>Vehicle</dt>
        <dd>{vehicle}</dd>
      </div>
      {vin && (
        <div>
          <dt>VIN</dt>
          <dd className={styles.code}>{vin}</dd>
        </div>
      )}
    </>
  );
}

export function PersonIdentityFacts({
  role,
  name,
  mobile,
  email,
  linked = true,
}: {
  role: string;
  name: string;
  mobile: string;
  email: string;
  // Contact opens the phone or mail app; off where the record is read rather than acted on.
  linked?: boolean;
}) {
  return (
    <>
      <div>
        <dt>{role}</dt>
        <dd>{name}</dd>
      </div>
      <div>
        <dt>Mobile</dt>
        <dd>
          {mobile && linked ? (
            <a className={styles.link} href={`tel:${mobile.replace(/[^\d+]/g, "")}`}>
              {mobile}
            </a>
          ) : (
            mobile || "Not on file"
          )}
        </dd>
      </div>
      <div>
        <dt>Email</dt>
        <dd>
          {email && linked ? (
            <a className={styles.link} href={`mailto:${email}`}>
              {email}
            </a>
          ) : (
            email || "Not on file"
          )}
        </dd>
      </div>
    </>
  );
}

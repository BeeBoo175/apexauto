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
}: {
  role: string;
  name: string;
  mobile: string;
  email: string;
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
          {mobile ? (
            <a className={styles.link} href={`tel:${mobile.replace(/[^\d+]/g, "")}`}>
              {mobile}
            </a>
          ) : (
            "Not on file"
          )}
        </dd>
      </div>
      <div>
        <dt>Email</dt>
        <dd>
          {email ? (
            <a className={styles.link} href={`mailto:${email}`}>
              {email}
            </a>
          ) : (
            "Not on file"
          )}
        </dd>
      </div>
    </>
  );
}

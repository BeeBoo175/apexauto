import styles from "./PhotoPlaceholder.module.css";

// The one placeholder behind every vehicle photograph. Put it first inside an element that uses the `frame`
// class from PhotoPlaceholder.module.css, and pass whether the photograph is still loading.
export function PhotoPlaceholder({ loading }: { loading: boolean }) {
  return (
    <span className={styles.placeholder} data-shell={loading || undefined} aria-hidden="true" />
  );
}

import type { ReactNode } from "react";
import Image from "next/image";
import detailPhoto from "../../../public/images/apexauto-detail.webp";
import styles from "./AuthPage.module.css";

interface AuthPageProps {
  title: string;
  introduction: string;
  children: ReactNode;
  links?: ReactNode;
}

export function AuthPage({ title, introduction, children, links }: AuthPageProps) {
  return (
    <section className={styles.page} aria-labelledby="auth-title">
      <div className={styles.formRegion}>
        <div className={styles.panel}>
          <div className={styles.heading}>
            <h1 id="auth-title">{title}</h1>
            {introduction && <p>{introduction}</p>}
          </div>
          {children}
          {links && (
            <nav className={styles.links} aria-label="Account access">
              {links}
            </nav>
          )}
        </div>
      </div>
      <div className={styles.photograph}>
        <div className={styles.imageContainer}>
          <Image
            src={detailPhoto}
            alt=""
            fill
            sizes="(max-width: 820px) 100vw, 50vw"
            loading="lazy"
            fetchPriority="low"
            className={styles.image}
          />
        </div>
      </div>
    </section>
  );
}

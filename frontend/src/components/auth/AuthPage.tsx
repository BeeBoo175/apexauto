import type { ReactNode } from "react";
import Image from "next/image";
import { Card } from "@/components/ui/Card";
import styles from "./AuthPage.module.css";

interface AuthPageProps {
  title: string;
  introduction: string;
  photoHeading: string;
  photoDescription: string;
  children: ReactNode;
  links?: ReactNode;
}

export function AuthPage({
  title,
  introduction,
  photoHeading,
  photoDescription,
  children,
  links,
}: AuthPageProps) {
  return (
    <section className={styles.page} aria-labelledby="auth-title">
      <div className={styles.formRegion}>
        <Card className={styles.panel}>
          <div className={styles.heading}>
            <h1 id="auth-title">{title}</h1>
            <p>{introduction}</p>
          </div>
          {children}
          {links && (
            <nav className={styles.links} aria-label="Account access">
              {links}
            </nav>
          )}
        </Card>
      </div>
      <div className={styles.photograph}>
        <Image
          src="/images/apexauto-detail.avif"
          alt=""
          fill
          sizes="(max-width: 820px) 100vw, 50vw"
          preload
          className={styles.image}
        />
        <div className={styles.photoContent}>
          <p className={styles.photoHeading}>{photoHeading}</p>
          <p className={styles.photoDescription}>{photoDescription}</p>
        </div>
      </div>
    </section>
  );
}

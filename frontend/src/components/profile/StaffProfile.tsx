"use client";

import { useRef, useState } from "react";
import { UsernameDialog } from "@/components/auth/UsernameDialog";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { Avatar } from "@/components/ui/Avatar";
import { Badge } from "@/components/ui/Badge";
import { Button, buttonClassName } from "@/components/ui/Button";
import { useAccountPicture } from "@/hooks/use-account-picture";
import { useAccountUsername } from "@/hooks/use-account-username";
import { useEmailVerificationResend } from "@/hooks/use-email-verification-resend";
import type { AccountProfile } from "@/lib/profile/contracts";
import { roleNames } from "@/lib/auth/actor";
import tones from "@/components/ui/StatusTone.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./StaffProfile.module.css";

// The dashboard's own account page. Picture and username are the person's to change; name, work contact, role
// and branch are managed by an Administrator, so they read as facts rather than controls.
export function StaffProfile({ profile }: { profile: AccountProfile }) {
  const picture = useAccountPicture(profile.userId, profile.pictureId);
  const username = useAccountUsername(profile.username);
  const resend = useEmailVerificationResend({
    userId: profile.userId,
    legalName: profile.name,
    email: profile.email,
    emailVerified: profile.emailVerified,
  });
  const [usernameOpen, setUsernameOpen] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const place = [roleNames[profile.role], profile.branchName].filter(Boolean).join(" · ");

  return (
    <>
      <StaffCommandBar title="Account" />
      <div className={workbench.workspace} data-scroll-with-host="true">
        <StaffRecordSummary
          ariaLabel="Your account"
          label={place}
          value={profile.name}
          lead={
            <Avatar
              accountId={profile.userId}
              name={profile.name}
              pictureId={picture.pictureId}
              size="lg"
            />
          }
          status={
            <StaffStatusGroup label="Email status">
              <Badge
                className={`${tones.badge} ${profile.emailVerified ? tones.success : tones.warning}`}
              >
                {profile.emailVerified ? "Email verified" : "Email not verified"}
              </Badge>
            </StaffStatusGroup>
          }
        />
        <div className={workbench.body}>
          <div className={styles.sections}>
            {/* Account details: picture management and username */}
            <section className={styles.card} aria-label="Account details">
              <header className={styles.cardHeader}>
                <h2 className={styles.cardTitle}>Account details</h2>
                <p className={styles.cardDescription}>
                  Personal identity and profile picture for workspace activity.
                </p>
              </header>
              <div className={styles.cardBody}>
                <div className={styles.row}>
                  <div className={styles.rowInfo}>
                    <span className={styles.rowLabel}>Profile picture</span>
                    <span className={styles.rowNote}>
                      {picture.pictureId
                        ? "Shown beside your name across workspace activity."
                        : "No picture uploaded yet."}
                    </span>
                  </div>
                  <div className={styles.rowActions}>
                    <input
                      ref={fileInput}
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      className="sr-only"
                      aria-label="Choose a profile picture"
                      tabIndex={-1}
                      onChange={(event) => {
                        const file = event.target.files?.[0];
                        event.target.value = "";
                        if (file) {
                          void picture.upload(file);
                        }
                      }}
                    />
                    <Button
                      variant="secondary"
                      size="compact"
                      disabled={picture.busy}
                      onClick={() => fileInput.current?.click()}
                    >
                      {picture.pictureId ? "Change picture" : "Add picture"}
                    </Button>
                    {picture.pictureId && (
                      <Button
                        variant="ghost"
                        size="compact"
                        disabled={picture.busy}
                        onClick={() => void picture.remove()}
                      >
                        Remove picture
                      </Button>
                    )}
                  </div>
                </div>
                <div className={styles.row}>
                  <div className={styles.rowInfo}>
                    <span className={styles.rowLabel}>Username</span>
                    <span className={styles.handle}>
                      <span aria-hidden="true" className={styles.at}>
                        @
                      </span>
                      <span>{username.username}</span>
                    </span>
                  </div>
                  <div className={styles.rowActions}>
                    <Button
                      variant="secondary"
                      size="compact"
                      onClick={() => setUsernameOpen(true)}
                    >
                      Change username
                    </Button>
                  </div>
                </div>
              </div>
            </section>

            {/* Workplace & Contact: read-only staff credentials managed by Admin */}
            <section className={styles.card} aria-label="Workplace & Contact">
              <header className={styles.cardHeader}>
                <h2 className={styles.cardTitle}>Workplace & Contact</h2>
                <p className={styles.cardDescription}>
                  Official dealership role, branch assignment, and contact records.
                </p>
              </header>
              <div className={styles.cardBody}>
                <dl className={styles.factsGrid}>
                  <div className={styles.fact}>
                    <dt className={styles.factLabel}>Role</dt>
                    <dd className={styles.factValue}>{roleNames[profile.role]}</dd>
                  </div>
                  {profile.branchName && (
                    <div className={styles.fact}>
                      <dt className={styles.factLabel}>Branch</dt>
                      <dd className={styles.factValue}>{profile.branchName}</dd>
                    </div>
                  )}
                  {profile.mobile && (
                    <div className={styles.fact}>
                      <dt className={styles.factLabel}>Work mobile</dt>
                      <dd className={styles.factValue}>{profile.mobile}</dd>
                    </div>
                  )}
                  <div className={styles.fact}>
                    <dt className={styles.factLabel}>Work email</dt>
                    <dd className={styles.factValue}>{profile.email}</dd>
                  </div>
                </dl>
                <div className={styles.adminNotice}>
                  <span className={styles.noticeIcon} aria-hidden="true">
                    ℹ
                  </span>
                  <span>Name, email and work contact are managed by an Administrator.</span>
                </div>
              </div>
            </section>

            {/* Security: verification & password recovery */}
            <section className={styles.card} aria-label="Security">
              <header className={styles.cardHeader}>
                <h2 className={styles.cardTitle}>Security</h2>
                <p className={styles.cardDescription}>
                  Sign-in credentials, email verification, and recovery options.
                </p>
              </header>
              <div className={styles.cardBody}>
                {!profile.emailVerified && (
                  <div className={styles.row}>
                    <div className={styles.rowInfo}>
                      <span className={styles.rowLabel}>Email verification</span>
                      <span className={styles.rowNote}>
                        Verify your email to access all operational tools in the staff workspace.
                      </span>
                    </div>
                    <div className={styles.rowActions}>
                      <Button
                        variant="secondary"
                        size="compact"
                        disabled={resend.busy}
                        onClick={() => void resend.resend()}
                      >
                        {resend.busy ? "Requesting email" : "Resend verification email"}
                      </Button>
                    </div>
                  </div>
                )}
                <div className={styles.row}>
                  <div className={styles.rowInfo}>
                    <span className={styles.rowLabel}>Password</span>
                    <span className={styles.rowNote}>Reset your password by email.</span>
                  </div>
                  <div className={styles.rowActions}>
                    <a href="/forgot-password" className={buttonClassName("secondary", "compact")}>
                      Password recovery
                    </a>
                  </div>
                </div>
              </div>
            </section>
          </div>
        </div>
      </div>
      <UsernameDialog
        account={username}
        open={usernameOpen}
        onClose={() => setUsernameOpen(false)}
      />
    </>
  );
}

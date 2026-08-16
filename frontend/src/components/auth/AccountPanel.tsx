"use client";

import { useRef, useState } from "react";
import { UsernameDialog } from "./UsernameDialog";
import { useAccountUsername } from "@/hooks/use-account-username";
import { Avatar } from "@/components/ui/Avatar";
import { Button, buttonClassName } from "@/components/ui/Button";
import { useAccountPicture } from "@/hooks/use-account-picture";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { useEmailVerificationResend } from "@/hooks/use-email-verification-resend";
import type { AccountProfile } from "@/lib/profile/contracts";
import { roleNames } from "@/lib/auth/actor";
import styles from "./AccountPanel.module.css";

export function AccountPanel({ profile }: { profile: AccountProfile }) {
  const isCustomer = profile.role === "Customer";
  const logout = useAuthMutation("logout", "/login");
  const resend = useEmailVerificationResend({
    userId: profile.userId,
    legalName: profile.name,
    email: profile.email,
    emailVerified: profile.emailVerified,
  });
  const verifiedPurpose = isCustomer
    ? "Your email is verified for reservations and inspection bookings."
    : "Your email is verified for the staff workspace.";
  const unverifiedPurpose = isCustomer
    ? "Verify your email to reserve a car or book an inspection."
    : "Verify your email to use the staff workspace.";
  const picture = useAccountPicture(profile.userId, profile.pictureId);
  const username = useAccountUsername(profile.username);
  const [usernameOpen, setUsernameOpen] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  let logoutLabel = "Sign out";

  if (logout.uncertain) {
    logoutLabel = "Retry sign out";
  }

  if (logout.busy) {
    logoutLabel = "Signing out";
  }

  return (
    <div className={styles.panel}>
      <header className={styles.header}>
        <h2 id="account-details-title">Account details</h2>
        <p className={styles.help}>Your sign-in identity and security.</p>
      </header>
      <div className={styles.profile}>
        <Avatar
          accountId={profile.userId}
          name={profile.name}
          pictureId={picture.pictureId}
          size="lg"
        />
        <div className={styles.identity}>
          <p className={styles.name}>{profile.name}</p>
          <p className={styles.email}>{profile.email}</p>
          <div className={styles.pictureActions}>
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
              size="sm"
              disabled={picture.busy}
              onClick={() => fileInput.current?.click()}
            >
              {picture.pictureId ? "Change picture" : "Add picture"}
            </Button>
            {picture.pictureId && (
              <Button
                variant="ghost"
                size="sm"
                disabled={picture.busy}
                onClick={() => void picture.remove()}
              >
                Remove picture
              </Button>
            )}
          </div>
        </div>
        <span className={styles.verificationState} data-verified={profile.emailVerified}>
          {profile.emailVerified ? "Verified" : "Not verified"}
        </span>
      </div>
      <dl className={styles.rows}>
        <div className={styles.row}>
          <dt className={styles.label}>Username</dt>
          <dd className={styles.value}>{username.username}</dd>
          <dd className={styles.control}>
            <Button variant="secondary" onClick={() => setUsernameOpen(true)}>
              Change username
            </Button>
          </dd>
        </div>
        {!isCustomer && (
          <div className={styles.row}>
            <dt className={styles.label}>Role</dt>
            <dd className={styles.value}>{roleNames[profile.role]}</dd>
          </div>
        )}
        {profile.branchName && (
          <div className={styles.row}>
            <dt className={styles.label}>Branch</dt>
            <dd className={styles.value}>{profile.branchName}</dd>
          </div>
        )}
        {profile.mobile && (
          <div className={styles.row}>
            <dt className={styles.label}>Work mobile</dt>
            <dd className={styles.value}>{profile.mobile}</dd>
          </div>
        )}
        <div className={styles.row}>
          <dt className={styles.label}>Email verification</dt>
          <dd className={styles.value}>
            <span className={styles.help}>
              {profile.emailVerified ? verifiedPurpose : unverifiedPurpose}
            </span>
          </dd>
          {!profile.emailVerified && (
            <dd className={styles.control}>
              <Button
                variant="secondary"
                disabled={resend.busy || logout.busy || logout.uncertain}
                onClick={() => void resend.resend()}
              >
                {resend.busy ? "Requesting email" : "Resend verification email"}
              </Button>
            </dd>
          )}
        </div>
        <div className={styles.row}>
          <dt className={styles.label}>Password</dt>
          <dd className={styles.value}>
            <span className={styles.help}>Reset your password by email.</span>
          </dd>
          <dd className={styles.control}>
            <a href="/forgot-password" className={buttonClassName("secondary", "md")}>
              Password recovery
            </a>
          </dd>
        </div>
        <div className={styles.row}>
          <dt className={styles.label}>Session</dt>
          <dd className={styles.value}>
            <span className={styles.help}>Sign out of ApexAuto on this device.</span>
          </dd>
          <dd className={styles.control}>
            <Button variant="secondary" disabled={logout.busy} onClick={() => void logout.submit()}>
              {logoutLabel}
            </Button>
          </dd>
        </div>
      </dl>
      <UsernameDialog
        account={username}
        open={usernameOpen}
        onClose={() => setUsernameOpen(false)}
      />
    </div>
  );
}

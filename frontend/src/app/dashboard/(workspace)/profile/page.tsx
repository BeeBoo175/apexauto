import { StaffProfile } from "@/components/profile/StaffProfile";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { readOwnProfile } from "@/lib/profile/server";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/profile");

export default function StaffProfilePage() {
  return (
    <StaffEntry
      path="/dashboard/profile"
      access={() => true}
      allowUnverified
      deniedTitle="Account unavailable"
      deniedDescription="Your account could not be opened."
    >
      {async (actor) => {
        const response = await readOwnProfile();
        if (!response.data || response.data.userId !== actor.userId) {
          throw new Error("Account service unavailable");
        }
        return (
          <>
            <LoadingProgressReady />
            <StaffProfile key={response.data.userId} profile={response.data} />
          </>
        );
      }}
    </StaffEntry>
  );
}

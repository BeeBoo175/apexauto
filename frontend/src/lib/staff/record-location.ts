import { isUuid } from "@/lib/auth/contracts";

export function staffRecordLocation(pathname: string, workspace: string) {
  if (pathname === workspace) {
    return { id: "", section: "" };
  }
  if (!pathname.startsWith(`${workspace}/`)) {
    return null;
  }
  const [id, section = "", extra] = pathname.slice(workspace.length + 1).split("/");
  if (!isUuid(id) || extra !== undefined) {
    return null;
  }
  return { id, section };
}

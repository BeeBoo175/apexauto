import { isUuid } from "@/lib/auth/contracts";
import { parseRoomUrl, roomUrlValues, type RoomView } from "@/lib/performance/room";

export const homeWorkspace = "/dashboard/home";

export type HomeState = { branchId: string; room: RoomView };

// Shape: /dashboard/home[?branchId=&team=workshop&search=&page=&pageSize=]. Defaults never appear in the address.
export function homeHref({ branchId, room }: HomeState) {
  const query = new URLSearchParams();
  const values = { branchId, ...roomUrlValues(room) };
  for (const [key, value] of Object.entries(values)) {
    if (value) {
      query.set(key, value);
    }
  }
  return query.size ? `${homeWorkspace}?${query}` : homeWorkspace;
}

// A branch choice only exists for the general manager; every other actor is scoped by the server.
export function homeLocation(pathname: string, search: string, general: boolean) {
  if (pathname !== homeWorkspace) {
    return null;
  }
  const parameters = Object.fromEntries(new URLSearchParams(search));
  const state: HomeState = {
    branchId: general && isUuid(parameters.branchId) ? parameters.branchId : "",
    room: parseRoomUrl(parameters),
  };
  return { ...state, href: homeHref(state) };
}

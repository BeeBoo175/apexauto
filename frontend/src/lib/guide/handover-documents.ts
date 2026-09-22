import type { IconName } from "@/lib/icons";

export const handoverDocuments = [
  {
    icon: "user" as IconName,
    title: "Original MyKad / NRIC",
    detail: "Required for JPJ thumbprint verification at transfer.",
  },
  {
    icon: "car" as IconName,
    title: "Valid driving licence",
    detail: "Active Competent Driving Licence (CDL) for test drives & pickup.",
  },
  {
    icon: "file-text" as IconName,
    title: "3 months income documents",
    detail: "Latest payslips plus 3 months bank or EPF statements for hire-purchase.",
  },
  {
    icon: "clock" as IconName,
    title: "Settlement letter (if trading in)",
    detail: "Current hire-purchase balance statement if your trade-in still has a loan.",
  },
] as const;

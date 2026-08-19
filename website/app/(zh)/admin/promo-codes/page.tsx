import type { Metadata } from "next";
import { AdminPromoCodes } from "@/components/AdminPromoCodes";

export const metadata: Metadata = {
  title: "促销代码管理",
  robots: { index: false, follow: false }
};

export default function AdminPromoCodesPage() {
  return <AdminPromoCodes />;
}

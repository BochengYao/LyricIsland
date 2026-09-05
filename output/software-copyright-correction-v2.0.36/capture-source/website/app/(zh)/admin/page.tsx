import type { Metadata } from "next";
import { AdminIncentives } from "@/components/AdminIncentives";

export const metadata: Metadata = {
  title: "用户反馈后台",
  robots: { index: false, follow: false }
};

export default function AdminPage() {
  return <AdminIncentives />;
}

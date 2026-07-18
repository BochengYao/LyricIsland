type Props = {
  className?: string;
  variant?: "default" | "nav";
};

export function ExternalArrow({ className, variant = "default" }: Props) {
  return (
    <span className={["externalArrow", className].filter(Boolean).join(" ")} aria-hidden="true">
      <svg viewBox={variant === "nav" ? "0 0 18 18" : "0 0 24 24"} focusable="false">
        <path d={variant === "nav" ? "M3.5 14.5 14.5 3.5M7 3.5h7.5V11" : "M5 19 19 5M9 5h10v10"} />
      </svg>
    </span>
  );
}

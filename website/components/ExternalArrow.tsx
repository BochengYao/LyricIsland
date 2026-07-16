type Props = {
  className?: string;
};

export function ExternalArrow({ className }: Props) {
  return (
    <span className={["externalArrow", className].filter(Boolean).join(" ")} aria-hidden="true">
      <svg viewBox="0 0 24 24" focusable="false">
        <path d="M5 19 19 5M9 5h10v10" />
      </svg>
    </span>
  );
}

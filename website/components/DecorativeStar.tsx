export function DecorativeStar({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 64 64" width="1em" height="1em" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" focusable="false">
      <path d="M32 4v56M4 32h56M12 12l40 40M12 52l40-40" />
    </svg>
  );
}

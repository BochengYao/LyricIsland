import type { SiteCopy } from "@/data/site-copy";
import { LyricDockIntroMotion } from "./LyricDockIntroMotion";
import { LyricDockProductScene } from "./LyricDockProductScene";
import styles from "./LyricDockShowcase.module.css";

type Props = {
  copy: SiteCopy["lyricDock"];
};

export function LyricDockShowcase({ copy }: Props) {
  return (
    <section className={styles.section} id="lyric-dock" data-snap-section>
      <LyricDockIntroMotion />
      <div className={`sectionContainer ${styles.inner}`}>
        <div className={styles.intro}>
          <div>
            <p className="eyebrow" data-lyric-dock-reveal="eyebrow">
              <span aria-hidden="true">•</span>
              {copy.eyebrow}
            </p>
            <h2 data-lyric-dock-reveal="title">{copy.title}</h2>
            <p className={styles.body} data-lyric-dock-reveal="body">
              {copy.body.split("\n").map((line) => (
                <span key={line}>{line}</span>
              ))}
            </p>
          </div>
        </div>

        <LyricDockProductScene alt={copy.imageAlt} />
      </div>

      <svg
        className={styles.orbitLine}
        viewBox="0 0 1440 900"
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        <path d="M -90 690 C 320 590, 650 680, 910 470 C 1170 260, 1350 180, 1600 530" />
        <circle cx="910" cy="470" r="4" />
      </svg>
    </section>
  );
}

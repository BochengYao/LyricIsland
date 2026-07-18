"use client";

import { useEffect, useRef, useState } from "react";

type Props = {
  label: string;
  players: string[];
};

type OrbitGeometry = {
  width: number;
  centerX: number;
  centerY: number;
  outerRadiusX: number;
  outerRadiusY: number;
  innerRadiusX: number;
  innerRadiusY: number;
};

type ArcSample = {
  angle: number;
  length: number;
};

const ORBIT_HEIGHT = 340;
const ORBIT_CENTER_Y = 260;
const ORBIT_DURATION_MS = 36_000;
const SAMPLE_COUNT = 720;

const initialGeometry: OrbitGeometry = {
  width: 1000,
  centerX: 500,
  centerY: ORBIT_CENTER_Y,
  outerRadiusX: 390,
  outerRadiusY: 220,
  innerRadiusX: 280,
  innerRadiusY: 160
};

function buildArcSamples(radiusX: number, radiusY: number) {
  const samples: ArcSample[] = [{ angle: Math.PI, length: 0 }];
  let totalLength = 0;
  let previousX = -radiusX;
  let previousY = 0;

  for (let index = 1; index <= SAMPLE_COUNT; index += 1) {
    const angle = Math.PI + (Math.PI * 2 * index) / SAMPLE_COUNT;
    const x = radiusX * Math.cos(angle);
    const y = radiusY * Math.sin(angle);
    totalLength += Math.hypot(x - previousX, y - previousY);
    samples.push({ angle, length: totalLength });
    previousX = x;
    previousY = y;
  }

  return { samples, totalLength };
}

function pointAtProgress(geometry: OrbitGeometry, samples: ArcSample[], totalLength: number, progress: number) {
  const targetLength = progress * totalLength;
  let low = 0;
  let high = samples.length - 1;

  while (low < high) {
    const middle = Math.floor((low + high) / 2);
    if (samples[middle].length < targetLength) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }

  const upper = samples[low];
  const lower = samples[Math.max(0, low - 1)];
  const span = upper.length - lower.length;
  const ratio = span > 0 ? (targetLength - lower.length) / span : 0;
  const angle = lower.angle + (upper.angle - lower.angle) * ratio;

  return {
    x: geometry.centerX + geometry.outerRadiusX * Math.cos(angle),
    y: geometry.centerY + geometry.outerRadiusY * Math.sin(angle)
  };
}

export function PlayerOrbit({ label, players }: Props) {
  const orbitPlayers = players.slice(0, -1);
  const centerPlayer = players.at(-1);
  const shellRef = useRef<HTMLDivElement>(null);
  const pillRefs = useRef<Array<HTMLSpanElement | null>>([]);
  const geometryRef = useRef(initialGeometry);
  const [geometry, setGeometry] = useState(initialGeometry);

  useEffect(() => {
    const shell = shellRef.current;
    if (!shell || orbitPlayers.length === 0) {
      return;
    }

    const motionQuery = window.matchMedia("(min-width: 1024px) and (prefers-reduced-motion: no-preference)");
    let animationFrame = 0;
    let startedAt = window.performance.now();
    let arc = buildArcSamples(initialGeometry.outerRadiusX, initialGeometry.outerRadiusY);

    const measure = () => {
      const pills = pillRefs.current.filter((pill): pill is HTMLSpanElement => Boolean(pill));
      const width = shell.clientWidth;
      const halfWidth = Math.max(72, ...pills.map((pill) => pill.offsetWidth / 2));
      const halfHeight = Math.max(26, ...pills.map((pill) => pill.offsetHeight / 2));
      const nextGeometry: OrbitGeometry = {
        width,
        centerX: width / 2,
        centerY: ORBIT_CENTER_Y,
        outerRadiusX: Math.max(width * 0.28, width / 2 - halfWidth - 14),
        outerRadiusY: ORBIT_CENTER_Y - halfHeight - 12,
        innerRadiusX: 0,
        innerRadiusY: 0
      };
      nextGeometry.innerRadiusX = Math.max(nextGeometry.outerRadiusX - 110, nextGeometry.outerRadiusX * 0.7);
      nextGeometry.innerRadiusY = Math.max(nextGeometry.outerRadiusY - 60, nextGeometry.outerRadiusY * 0.68);
      geometryRef.current = nextGeometry;
      arc = buildArcSamples(nextGeometry.outerRadiusX, nextGeometry.outerRadiusY);
      setGeometry(nextGeometry);
    };

    const clearInlineMotion = () => {
      pillRefs.current.forEach((pill) => {
        pill?.style.removeProperty("transform");
        pill?.style.removeProperty("opacity");
      });
    };

    const animate = (time: number) => {
      if (!motionQuery.matches) {
        clearInlineMotion();
        return;
      }

      const currentGeometry = geometryRef.current;
      const elapsedProgress = ((time - startedAt) / ORBIT_DURATION_MS) % 1;
      const playerCount = orbitPlayers.length;

      pillRefs.current.forEach((pill, index) => {
        if (!pill) {
          return;
        }

        const playerIndex = index % playerCount;
        const duplicateOffset = index >= playerCount ? 0.5 : 0;
        const phase = playerIndex / (playerCount * 2) + duplicateOffset;
        const progress = (elapsedProgress + phase) % 1;
        const point = pointAtProgress(currentGeometry, arc.samples, arc.totalLength, progress);
        const lowerDepth = Math.max(0, point.y - currentGeometry.centerY);
        const opacity = Math.max(0, 1 - lowerDepth / 42);

        pill.style.transform = `translate3d(${point.x}px, ${point.y}px, 0) translate(-50%, -50%)`;
        pill.style.opacity = opacity.toFixed(3);
      });

      animationFrame = window.requestAnimationFrame(animate);
    };

    const restartMotion = () => {
      window.cancelAnimationFrame(animationFrame);
      startedAt = window.performance.now();
      measure();
      if (motionQuery.matches) {
        animationFrame = window.requestAnimationFrame(animate);
      } else {
        clearInlineMotion();
      }
    };

    const resizeObserver = new ResizeObserver(measure);
    resizeObserver.observe(shell);
    motionQuery.addEventListener("change", restartMotion);
    restartMotion();

    return () => {
      window.cancelAnimationFrame(animationFrame);
      resizeObserver.disconnect();
      motionQuery.removeEventListener("change", restartMotion);
    };
  }, [orbitPlayers.length]);

  const visualPlayers = [...orbitPlayers, ...orbitPlayers];

  return (
    <div className="playerOrbit" aria-label={label} ref={shellRef}>
      <svg
        className="playerArcLines"
        viewBox={`0 0 ${geometry.width} ${ORBIT_HEIGHT}`}
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        <ellipse
          cx={geometry.centerX}
          cy={geometry.centerY}
          rx={geometry.outerRadiusX}
          ry={geometry.outerRadiusY}
        />
        <ellipse
          cx={geometry.centerX}
          cy={280}
          rx={geometry.innerRadiusX}
          ry={geometry.innerRadiusY}
        />
      </svg>
      {visualPlayers.map((player, index) => (
        <span
          aria-hidden={index >= orbitPlayers.length ? "true" : undefined}
          className={`playerPill playerPill${(index % orbitPlayers.length) + 1}${index >= orbitPlayers.length ? " playerPillDuplicate" : ""}`}
          key={`${player}-${index}`}
          ref={(node) => {
            pillRefs.current[index] = node;
          }}
        >
          {player}
        </span>
      ))}
      {centerPlayer && <span className="playerPill playerPill7">{centerPlayer}</span>}
    </div>
  );
}

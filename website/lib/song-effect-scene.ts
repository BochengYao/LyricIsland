import type { EasterSong } from "@/data/easter-songs";

// Browser port of the v3.7.2 Fullscreen/*Renderer.cs formulas.
// Catalog geometry, colors, sizes, densities and travel times are kept intact.
// Container-sized rendering for the quiz and full-screen preview. No DOM masks.
type Asset = { path: Path2D; width: number; height: number };
type Spark = { angle: number; width: number; height: number; color: string; distance: number };
type Particle = {
  asset: Asset; x: number; y: number; w: number; h: number; color: string; opacity: number;
  phase: number; phaseSpeed: number; pulse: number; rotation: number; rotationSpeed: number;
  vx: number; vy: number; amplitude: number; direction: number; age: number; duration: number;
  horizontal: boolean; cross: number; length: number; thickness: number; wavelength: number;
  originX: number; originY: number; shade: number; flipPhase: number; lineStart: number; lines: number[]; sparks: Spark[];
};
const rad = Math.PI / 180;
const range = (a: number, b: number) => a + Math.random() * (b - a);
const pick = <T,>(array: T[]) => array[Math.floor(Math.random() * array.length)];

export async function createSongEffectScene(canvas: HTMLCanvasElement, song: EasterSong, signal: AbortSignal, onReady: () => void, options: { scrollWithPage?: boolean } = {}) {
  const assets = await Promise.all(song.assets.map(async name => {
    const response = await fetch(`/images/song-effects/${name}`, { signal });
    if (!response.ok) throw new Error(`Missing effect asset: ${name}`);
    const svg = new DOMParser().parseFromString(await response.text(), "image/svg+xml");
    const viewBox = svg.documentElement.getAttribute("viewBox")?.split(/\s+/).map(Number);
    const geometry = [...svg.querySelectorAll("path")].map(path => path.getAttribute("d") || "").join(" ");
    if (!viewBox || viewBox.length !== 4 || !geometry) throw new Error(`Invalid effect asset: ${name}`);
    return { path: new Path2D(geometry), width: viewBox[2], height: viewBox[3] };
  }));
  if (signal.aborted) return () => {};
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("Canvas unavailable");
  const effect = song.definition;
  const type = song.effect;
  const isLeaf = type === "maple-leaf-fall";
  const isPaper = type === "flying-paper-storm";
  const isRain = type === "rain-streaks";
  const isSnake = type === "snake-crawl";
  const isFire = type === "edge-fireworks";
  const isBat = type === "bat-swarm";
  const isNeon = type === "neon-speed-lines";
  const media = matchMedia("(prefers-reduced-motion: reduce)");
  let width = canvas.clientWidth, height = canvas.clientHeight, ratio = 1;
  let particles: Particle[] = [];
  let raf = 0, last = performance.now(), nextSpawn = 0, paused = false, dirty = true;
  // The bitmap stays viewport-sized. Particle positions belong to the document,
  // so scrolling translates existing artwork instead of restarting it or
  // allocating a canvas as tall as the entire page.
  const origin = options.scrollWithPage ? window.scrollY : 0;
  let scrollOffset = 0, lastFillOffset = 0;
  let worldTop = 0, worldBottom = height;
  const resize = () => {
    const bounds = canvas.getBoundingClientRect();
    width = Math.max(1, bounds.width); height = Math.max(1, bounds.height);
    ratio = Math.min(devicePixelRatio || 1, 2);
    canvas.width = Math.round(width * ratio); canvas.height = Math.round(height * ratio);
    dirty = true;
  };
  const edgeX = (size: number) => {
    const max = Math.max(0, width - size);
    if (Math.random() < .64) return Math.random() < .5 ? range(0, max * .28) : max * range(.72, 1);
    return range(0, max);
  };
  const target = () => width * height >= 6000000 ? effect.count4k : width * height >= 2800000 ? effect.count1440p : effect.count1080p;
  const delay = () => isLeaf ? range(1.2, 2.2) : isPaper ? range(.24, .58) : isRain ? range(.07, .17) : isSnake ? range(1.8, 3.2) : isFire ? range(.7, 1.6) : isNeon ? range(.45, 1) : isBat ? range(.75, 1.5) : range(.85, 1.7);
  function spawn(initial: boolean): Particle {
    const asset = pick(assets), size = range(effect.minSize, effect.maxSize);
    const p: Particle = {
      asset, x: 0, y: 0, w: size, h: size * asset.height / asset.width, color: pick(effect.colors), opacity: range(effect.minOpacity, effect.maxOpacity),
      phase: range(0, Math.PI * 2), pulse: range(0, Math.PI * 2), phaseSpeed: range(.45, 1.1), rotation: range(-52, 52), rotationSpeed: range(-10, 10),
      vx: 0, vy: 0, amplitude: range(5, 18), direction: 1, age: 0, duration: 1, horizontal: true, cross: 0, length: size, thickness: 0, wavelength: 0,
      originX: .5, originY: .5, shade: range(.34, .68), flipPhase: range(0, Math.PI * 2), lineStart: range(.18, .26), lines: [], sparks: [],
    };
    if (isLeaf || isRain) {
      if (isRain) { p.h = size; p.w = Math.max(2.5, size * asset.width / asset.height); p.rotation = 0; p.vx = range(-26, 8); }
      p.x = edgeX(p.w); p.y = initial ? range(0, height * (isRain ? 1 : .55)) - size : -size;
      p.vy = (height + size * 2) / (isRain ? range(3.4, 6.6) : range(20, 35));
      p.cross = p.x;
    } else if (isSnake) {
      p.thickness = 8.5 + (size - effect.minSize) / Math.max(1, effect.maxSize - effect.minSize) * 4;
      p.horizontal = Math.random() < .74; p.direction = Math.random() < .68 ? 1 : -1;
      const span = p.horizontal ? width : height, edge = range(26, 78);
      p.cross = Math.random() < .5 ? edge : (p.horizontal ? height : width) - edge;
      p.x = initial ? range(0, span) : p.direction > 0 ? -p.thickness * 4 : span + p.thickness * 4;
      p.amplitude = range(11, 17); p.wavelength = range(58, 84); p.phaseSpeed = range(.55, 1);
      p.vx = (span + size * 2) / range(15, 21);
    } else if (isFire) {
      p.w = p.h = size;
      p.x = width * (particles.length % 2 === 0 ? range(.37, .45) : range(.82, .92));
      p.y = height * (particles.length % 2 === 0 || Math.random() < .82 ? range(.08, .24) : range(.68, .81));
      p.duration = range(4.2, 5.1); p.age = initial ? range(.58, .78) : 0;
      const rotation = range(0, Math.PI * 2);
      p.sparks = Array.from({ length: 24 }, (_, i) => {
        const outer = i < 16, n = outer ? 16 : 8, index = outer ? i : i - 16;
        const h = Math.max(outer ? 10 : 7, size * (outer ? .082 : .055));
        return { angle: rotation + Math.PI * 2 * index / n + (outer ? 0 : Math.PI / n), height: h, width: Math.max(2.2, h * asset.width / asset.height), color: pick(effect.colors), distance: outer ? 1 : .58 };
      });
    } else {
      const scale = size / Math.max(asset.width, asset.height);
      p.w = Math.max(isPaper ? 8 : 6, asset.width * scale); p.h = Math.max(isPaper ? 8 : 4, asset.height * scale);
      p.direction = Math.random() < (isPaper ? .72 : .62) ? 1 : -1;
      p.x = initial ? range(isPaper ? -p.w : 0, width - (isPaper ? -p.w : p.w)) : p.direction > 0 ? -p.w * 1.5 : width + p.w * .5;
      const maxY = Math.max(0, height - Math.max(p.w, p.h));
      p.y = Math.random() < .68 ? (Math.random() < .5 ? range(0, maxY * .34) : maxY * range(.66, 1)) : range(0, maxY);
      p.vx = (width + p.w * 3) / (isPaper ? range(8.5, 16) : isNeon ? range(5.5, 10) : isBat ? range(9, 16) : range(18, 30)) * p.direction;
      p.vy = isPaper ? range(-.44, .56) * 15 : isNeon || isBat ? range(-3, 3) : range(-5, 5);
      p.amplitude = isPaper ? range(20, 20 + Math.min(120, height * .16)) : isNeon ? range(2, 7) : isBat ? range(12, 40) : range(5, 18);
      p.phaseSpeed = isPaper ? range(.55, 1.7) : isBat ? range(2.1, 3.9) : range(.45, 1.2);
      p.rotation = isPaper ? range(-35, 35) : isNeon ? range(-5, 5) : range(-24, 24);
      p.rotationSpeed = isPaper ? range(-52.5, 52.5) : isBat || isNeon ? 0 : range(-8, 8);
      if (isPaper) { p.originX = range(.28, .72); p.originY = range(.30, .70); p.lines = Math.random() < .22 ? [] : Array.from({ length: 2 + Math.floor(Math.random() * 4) }, () => range(.34, .72)); }
    }
    const offset = options.scrollWithPage ? window.scrollY - origin : 0;
    if (isSnake) { if (p.horizontal) p.cross += offset; else p.x += offset; }
    else p.y += offset;
    return p;
  }
  function drawAsset(p: Particle, x: number, y: number, sx = 1, sy = 1, skewX = 0, skewY = 0, curlX = 1, curlY = 1) {
    ctx!.save();
    ctx!.translate(x + p.w * p.originX, y + p.h * p.originY); ctx!.rotate(p.rotation * rad); ctx!.scale(sx, sy);
    ctx!.transform(1, Math.tan(skewY * rad), Math.tan(skewX * rad), 1, 0, 0);
    ctx!.scale(curlX, curlY);
    ctx!.translate(-p.w * p.originX, -p.h * p.originY);
    ctx!.save(); ctx!.scale(p.w / p.asset.width, p.h / p.asset.height);
    ctx!.fillStyle = p.color; ctx!.fill(p.asset.path);
    if (isNeon) { ctx!.strokeStyle = p.color; ctx!.lineWidth = 1.5 * p.asset.width / p.w; ctx!.stroke(p.asset.path); }
    if (isPaper) {
      const gradient = ctx!.createLinearGradient(0, 0, p.asset.width, 0);
      gradient.addColorStop(Math.max(0, p.shade - .24), "transparent"); gradient.addColorStop(p.shade, "rgba(40,40,43,.149)"); gradient.addColorStop(Math.min(1, p.shade + .22), "rgba(255,255,255,.031)");
      ctx!.fillStyle = gradient; ctx!.fill(p.asset.path);
    }
    ctx!.restore();
    if (isPaper) { ctx!.fillStyle = "rgba(43,43,45,.282)"; p.lines.forEach((line, i) => ctx!.fillRect(p.w * p.lineStart, p.h * (.22 + i * .115), p.w * line, Math.max(.8, p.h * .018))); }
    ctx!.restore();
  }
  function drawSnake(p: Particle) {
    const centers = Array.from({ length: 24 }, (_, i) => {
      const along = p.x - p.direction * (p.length - p.length / 23 * i), wave = along / p.wavelength * Math.PI * 2 + p.phase;
      const cross = p.cross + Math.sin(wave) * p.amplitude + Math.sin(wave * .47 + p.pulse) * p.amplitude * .24;
      return p.horizontal ? { x: along, y: cross } : { x: cross, y: along };
    });
    const edges = centers.map((point, i) => {
      const previous = centers[Math.max(0, i - 1)], next = centers[Math.min(23, i + 1)];
      const dx = next.x - previous.x, dy = next.y - previous.y, magnitude = Math.max(.001, Math.hypot(dx, dy));
      const progress = i / 23;
      let taper = Math.min(1, .10 + progress * 3.2); if (progress > .9) taper *= 1 - (progress - .9) * .55;
      const half = p.thickness * taper * .5 * (.96 + Math.sin(progress * Math.PI * 7 + p.phase) * .04);
      return [{ x: point.x - dy / magnitude * half, y: point.y + dx / magnitude * half }, { x: point.x + dy / magnitude * half, y: point.y - dx / magnitude * half }];
    });
    ctx!.beginPath(); [...edges.map(e => e[0]), ...edges.map(e => e[1]).reverse()].forEach((v, i) => i ? ctx!.lineTo(v.x, v.y) : ctx!.moveTo(v.x, v.y));
    ctx!.closePath(); ctx!.fillStyle = p.color; ctx!.fill(); ctx!.strokeStyle = "rgba(226,226,226,.863)"; ctx!.lineWidth = 1.35; ctx!.stroke();
    for (let i = 0; i < 5; i++) {
      const index = Math.min(22, 6 + i * 3), point = centers[index], next = centers[index + 1];
      const w = p.thickness * .9, h = p.thickness * .58;
      ctx!.save(); ctx!.translate(point.x, point.y); ctx!.rotate(Math.atan2(next.y - point.y, next.x - point.x)); ctx!.strokeStyle = "rgba(242,242,242,.843)"; ctx!.lineWidth = Math.max(.85, p.thickness * .1);
      ctx!.beginPath(); ctx!.moveTo(-w / 2, -h / 2); ctx!.lineTo(0, h / 2); ctx!.lineTo(w / 2, -h / 2); ctx!.stroke(); ctx!.restore();
    }
    const head = centers[23], previous = centers[22], w = p.thickness * 3.45, h = p.thickness * 2.25;
    ctx!.save(); ctx!.translate(head.x, head.y); ctx!.rotate(Math.atan2(head.y - previous.y, head.x - previous.x)); ctx!.translate(-w / 2, -h / 2);
    ctx!.save(); ctx!.scale(w / p.asset.width, h / p.asset.height); ctx!.fillStyle = p.color; ctx!.fill(p.asset.path); ctx!.strokeStyle = "rgba(226,226,226,.863)"; ctx!.lineWidth = 1.1 * p.asset.width / w; ctx!.stroke(p.asset.path); ctx!.restore();
    ctx!.strokeStyle = "rgba(246,246,246,.882)"; ctx!.lineWidth = Math.max(1, p.thickness * .12);
    ctx!.beginPath(); ctx!.moveTo(w * .54, h * .29); ctx!.lineTo(w * .76, h * .39); ctx!.moveTo(w * .54, h * .71); ctx!.lineTo(w * .76, h * .61); ctx!.stroke();
    ctx!.lineWidth = Math.max(.9, p.thickness * .09); ctx!.beginPath(); ctx!.moveTo(w * .96, h * .5); ctx!.lineTo(w * 1.2, h * .36); ctx!.moveTo(w * .96, h * .5); ctx!.lineTo(w * 1.2, h * .64); ctx!.stroke(); ctx!.restore();
  }
  function drawFire(p: Particle) {
    const progress = Math.min(1, p.age / p.duration), eased = 1 - (1 - progress) ** 2.4;
    const radius = p.w * .39 * eased, fade = progress < .34 ? progress / .34 : 1 - (progress - .34) / .66;
    ctx!.globalAlpha = p.opacity * Math.max(0, fade);
    p.sparks.forEach(spark => {
      const x = p.x + Math.cos(spark.angle) * radius * spark.distance, y = p.y + Math.sin(spark.angle) * radius * spark.distance;
      ctx!.save(); ctx!.translate(x, y); ctx!.rotate(spark.angle + Math.PI / 2); ctx!.scale(spark.width / p.asset.width, spark.height / p.asset.height); ctx!.translate(-p.asset.width / 2, -p.asset.height / 2); ctx!.fillStyle = spark.color; ctx!.fill(p.asset.path); ctx!.restore();
    });
    ctx!.fillStyle = "white"; ctx!.beginPath(); ctx!.arc(p.x, p.y, Math.max(6, p.w * .042) / 2, 0, Math.PI * 2); ctx!.fill();
  }
  const centerY = (p: Particle) => isSnake ? (p.horizontal ? p.cross : p.x - p.direction * p.length * .5) : p.y;
  function render(now: number) {
    const frozen = paused || media.matches || document.hidden;
    const dt = frozen ? 0 : Math.min(.1, (now - last) / 1000); last = now;
    if (!frozen || dirty) {
      dirty = false; ctx!.setTransform(ratio, 0, 0, ratio, 0, 0); ctx!.clearRect(0, 0, width, height);
      scrollOffset = options.scrollWithPage ? window.scrollY - origin : 0;
      worldTop = options.scrollWithPage ? -origin : 0;
      worldBottom = options.scrollWithPage ? Math.max(height, document.documentElement.scrollHeight) - origin : height;
      ctx!.translate(0, -scrollOffset);
      if (options.scrollWithPage && !frozen && Math.abs(scrollOffset - lastFillOffset) > height * .4) {
        particles = particles.filter(p => Math.abs(centerY(p) - scrollOffset - height * .5) < height * 2).slice(-target() * 2);
        const visible = particles.filter(p => centerY(p) >= scrollOffset - p.h && centerY(p) <= scrollOffset + height + p.h).length;
        for (let n = visible; n < target(); n++) particles.push(spawn(true));
        lastFillOffset = scrollOffset;
      }
      if (options.scrollWithPage) particles = particles.filter(p => Math.abs(centerY(p) - scrollOffset - height * .5) < height * 2.5).slice(-target() * 3);
      const visibleCount = options.scrollWithPage ? particles.filter(p => centerY(p) >= scrollOffset - p.h && centerY(p) <= scrollOffset + height + p.h).length : particles.length;
      if (!frozen && visibleCount < target() && now >= nextSpawn) { particles.push(spawn(false)); nextSpawn = now + delay() * 1000; }
      particles = particles.filter(p => {
        ctx!.globalAlpha = media.matches ? Math.min(.18, p.opacity) : p.opacity;
        p.age += dt; p.phase += p.phaseSpeed * dt;
        if (isSnake) {
          p.x += p.vx * p.direction * (.94 + Math.sin(p.phase * .55 + p.pulse) * .06) * dt;
          drawSnake(p); const span = p.horizontal ? width : worldBottom, tail = p.x - p.direction * p.length;
          return p.direction > 0 ? tail <= span + p.thickness * 6.9 : tail >= (p.horizontal ? 0 : worldTop) - p.thickness * 6.9;
        }
        if (isFire) { drawFire(p); return p.age < p.duration; }
        if (isLeaf) {
          p.y += p.vy * dt; p.rotation += p.rotationSpeed * dt; drawAsset(p, p.cross + Math.sin(p.phase) * p.amplitude, p.y);
          return p.y <= worldBottom + p.h;
        }
        if (isRain) { p.x += p.vx * dt; p.y += p.vy * dt; drawAsset(p, p.x, p.y); return p.y <= worldBottom + p.h && p.x >= -p.w * 3 && p.x <= width + p.w * 3; }
        const gust = isPaper ? .76 + (Math.sin(p.phase * .72 + p.pulse) + 1) * .28 : 1;
        p.x += p.vx * gust * dt; p.y += p.vy * dt;
        const y = p.y + Math.sin(p.phase) * p.amplitude + (isPaper ? Math.sin(p.phase * 2.37 + p.pulse) * p.amplitude * .24 : 0);
        p.rotation += p.rotationSpeed * gust * dt + (isPaper ? Math.sin(p.phase * 1.84) * 18 * dt : 0);
        if (isPaper) {
          let flip = Math.cos(p.phase * 1.16 + p.flipPhase); if (Math.abs(flip) < .1) flip = flip < 0 ? -.1 : .1;
          drawAsset(p, p.x, y, flip, .55 + Math.cos(p.phase * .67 + p.pulse) * .34, Math.sin(p.phase * .82 + p.flipPhase) * 18, Math.cos(p.phase * 1.11 + p.pulse) * 12, .86 + Math.sin(p.phase * 1.43 + p.pulse) * .14, .90 + Math.cos(p.phase * 1.67 + p.flipPhase) * .1);
        } else if (isBat) { p.rotation = Math.sin(p.phase) * 8; drawAsset(p, p.x, y, 1, .58 + Math.abs(Math.sin(p.phase * 2.4)) * .48); }
        else if (isNeon) drawAsset(p, p.x, y);
        else { const pulse = .86 + Math.sin(p.phase * .8 + p.pulse) * .14; drawAsset(p, p.x, y, pulse, pulse); }
        return (p.vx >= 0 ? p.x <= width + p.w * 2 : p.x >= -p.w * 2) && y >= worldTop - p.h * 3 && y <= worldBottom + p.h * 3;
      });
      ctx!.globalAlpha = 1;

    }
    if (!signal.aborted && !document.hidden && !paused && !media.matches) raf = requestAnimationFrame(render);
  }
  function wake() { cancelAnimationFrame(raf); dirty = true; last = performance.now(); render(last); }
  function resetMotion() {
    scrollOffset = options.scrollWithPage ? window.scrollY - origin : 0;
    particles = Array.from({ length: Math.min(media.matches ? (isSnake || isFire ? 2 : 3) : target(), target()) }, () => spawn(true));
    if (media.matches) particles.forEach((p, index) => {
      p.opacity = Math.min(isRain ? .14 : isPaper || (!isLeaf && !isSnake && !isFire) ? .16 : .18, p.opacity);
      if (isSnake) { p.horizontal = index === 0; p.direction = index === 0 ? 1 : -1; p.cross = index === 0 ? Math.max(24, height - 72) + scrollOffset : Math.max(24, width - 58); p.x = index === 0 ? width * .34 : height * .68 + scrollOffset; }
      else if (isFire) p.age = p.duration * (.48 + index * .1);
      else {
        p.x = index === 1 ? Math.max(0, width - p.w - (isLeaf ? 30 : isRain ? 42 : 34)) : (isLeaf ? 24 + index * 36 : isRain || isPaper ? 28 + index * 48 : 26 + index * 52);
        p.cross = p.x;
        p.y = isRain ? 54 + height * index * .24 : isLeaf ? (index === 0 ? 46 : height * (.22 + index * .24)) : isPaper ? (index === 0 ? 54 : height * (.20 + index * .27)) : (index === 0 ? 42 : height * (.22 + index * .25));
        p.y += scrollOffset;
      }
    });
    wake();
  }
  const observer = new ResizeObserver(() => { resize(); wake(); });
  observer.observe(canvas);
  const onScroll = () => { dirty = true; if (paused || media.matches) wake(); };
  if (options.scrollWithPage) window.addEventListener("scroll", onScroll, { passive: true });
  document.addEventListener("visibilitychange", wake); media.addEventListener("change", resetMotion);
  // Explicit pause events avoid restarting the effect when controls change.
  const onPause = () => { paused = canvas.dataset.paused === "true"; wake(); };
  canvas.addEventListener("effectpause", onPause);
  resize(); resetMotion(); onReady();
  return () => {
    cancelAnimationFrame(raf); observer.disconnect();
    if (options.scrollWithPage) window.removeEventListener("scroll", onScroll);
    document.removeEventListener("visibilitychange", wake); media.removeEventListener("change", resetMotion); canvas.removeEventListener("effectpause", onPause);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
  };
}

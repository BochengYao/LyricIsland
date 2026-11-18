const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.BASE_URL || 'http://127.0.0.1:3218';
const a = 'taylor-swift-all-too-well-maple-leaves';
const b = 'wham-last-christmas-snowfall';
const c = 'katy-perry-firework-edge-bursts';
const selector = id => `canvas[data-background-effect="${id}"]`;

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, locale: 'zh-CN' });
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(base + '/songs/', { waitUntil: 'networkidle' });
  const row = id => page.locator(`[data-song-id="${id}"] button`);
  await row(a).click();
  await page.locator(`${selector(a)}[data-visible="true"]`).waitFor();
  await page.waitForFunction(selector => Number(getComputedStyle(document.querySelector(selector)).opacity) >= .64, selector(a));

  // Freeze time, not scrolling. Existing pixels must move by the same amount
  // as the document; opacity transitions and animation physics cannot hide a
  // viewport-locked background in this comparison.
  await page.locator(selector(a)).evaluate(el => {
    el.dataset.paused = 'true'; el.dispatchEvent(new Event('effectpause'));
    window.__motionReference = { pixels: el.getContext('2d').getImageData(0, 0, el.width, el.height).data, width: el.width, height: el.height, y: scrollY };
  });
  await page.evaluate(() => scrollTo({ top: scrollY + 120, behavior: 'instant' }));
  await page.waitForTimeout(100);
  const movement = await page.locator(selector(a)).evaluate(el => {
    const previous = window.__motionReference, pixels = el.getContext('2d').getImageData(0, 0, el.width, el.height).data;
    const dy = Math.round((scrollY - previous.y) * el.height / innerHeight);
    let painted = 0, differences = 0;
    for (let y = 0; y < el.height - dy; y++) for (let x = 0; x < el.width; x++) {
      const oldIndex = ((y + dy) * el.width + x) * 4, newIndex = (y * el.width + x) * 4;
      if (previous.pixels[oldIndex + 3] || pixels[newIndex + 3]) {
        painted++;
        if ([0,1,2,3].some(channel => Math.abs(previous.pixels[oldIndex + channel] - pixels[newIndex + channel]) > 1)) differences++;
      }
    }
    return { dy, painted, differences };
  });
  assert.equal(movement.dy, 120);
  assert.ok(movement.painted > 100);
  assert.ok(movement.differences < movement.painted * .01, JSON.stringify(movement));

  // A new scene appears while the old scene fades, then releases the old canvas.
  await row(b).click();
  await page.waitForFunction(([a,b]) => document.querySelector(a)?.dataset.exiting === 'true' && document.querySelector(b)?.dataset.visible === 'true', [selector(a), selector(b)]);
  await page.waitForTimeout(45);
  const overlap = await page.evaluate(([a,b]) => ({ old: Number(getComputedStyle(document.querySelector(a)).opacity), incoming: Number(getComputedStyle(document.querySelector(b)).opacity) }), [selector(a), selector(b)]);
  assert.ok(overlap.old > 0 && overlap.old < .65, JSON.stringify(overlap));
  assert.ok(overlap.incoming > 0 && overlap.incoming < 1, JSON.stringify(overlap));
  await page.locator(selector(a)).waitFor({ state: 'detached' });
  await page.waitForFunction(selector => Number(getComputedStyle(document.querySelector(selector)).opacity) > .99, selector(b));
  await row(b).click();
  await page.locator(`${selector(b)}[data-exiting="true"]`).waitFor();
  assert.equal(await row(b).getAttribute('aria-pressed'), 'false');
  await page.waitForTimeout(45);
  const fading = await page.locator(selector(b)).evaluate(el => Number(getComputedStyle(el).opacity));
  assert.ok(fading > 0 && fading < 1);
  await page.locator(selector(b)).waitFor({ state: 'detached' });

  // Rapid switching/cancellation must not reveal an obsolete late asset load.
  await row(a).click(); await row(b).click(); await row(c).click(); await row(c).click();
  await page.waitForFunction(() => !document.querySelector('canvas[data-background-effect]'));
  await page.waitForTimeout(250);
  assert.equal(await page.locator('canvas[data-background-effect]').count(), 0);
  assert.equal(await page.locator('[data-preview]').count(), 0);
  assert.deepEqual(errors, []);
  await browser.close();
  console.log('Passed: document-aligned pixels on scroll, live crossfade, fade-out before disposal, immediate row deselection and rapid-switch cancellation.');
})().catch(error => { console.error(error); process.exit(1); });

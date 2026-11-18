const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const catalog = require('../data/song-effects-catalog.json');
const path = require('node:path');
const base = process.env.BASE_URL || 'http://127.0.0.1:3218';
const output = path.resolve(__dirname, '../.screenshots-easter-songs');

async function settled(page) {
  await page.waitForFunction(() => {
    const all = document.querySelectorAll('canvas[data-background-effect]');
    if (all.length !== 1) return false;
    const canvas = all[0];
    const selected = document.querySelector('[data-song-id] button[aria-pressed="true"]')?.closest('[data-song-id]')?.dataset.songId;
    return canvas.dataset.backgroundEffect === selected && canvas.dataset.ready === 'true' && canvas.dataset.visible === 'true' && Number(getComputedStyle(canvas).opacity) >= (canvas.dataset.snow === 'true' ? .99 : .64);
  });
}
async function removed(page) { await page.waitForFunction(() => !document.querySelector('canvas[data-background-effect]')); }

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'zh-CN' });
  const page = await context.newPage(), errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(base + '/songs/', { waitUntil: 'networkidle' });
  const url = page.url();
  const hint = page.getByText('点击歌曲预览彩蛋🎉', { exact: true });
  for (const [width, height] of [[1440, 900], [390, 844], [800, 1200]]) {
    await page.setViewportSize({ width, height });
    const titleBox = await page.locator('#playlist-title').boundingBox(), hintBox = await hint.boundingBox();
    if (width > height) assert.ok(hintBox.x >= titleBox.x + titleBox.width + 10 && hintBox.y < titleBox.y + titleBox.height);
    else assert.ok(hintBox.y >= titleBox.y + titleBox.height + 6);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  const background = page.locator('canvas[data-background-effect]');
  for (const [index, entry] of catalog.entries.entries()) {
    const row = page.locator(`[data-song-id="${entry.id}"] button`);
    await row.scrollIntoViewIfNeeded();
    const scrollY = await page.evaluate(() => window.scrollY);
    await row.hover();
    const originalStyle = await row.evaluate(el => ({ background: getComputedStyle(el).backgroundColor, shadow: getComputedStyle(el).boxShadow }));
    if (index === 0) { await row.focus(); await page.keyboard.press('Enter'); }
    else await row.click();
    await page.locator(`canvas[data-background-effect="${entry.id}"][data-ready="true"]`).waitFor();
    await settled(page);
    assert.equal(page.url(), url);
    assert.equal(await page.getByRole('dialog').count(), 0);
    assert.equal(await page.evaluate(() => window.scrollY), scrollY, 'Selecting a song must not change page position');
    assert.notEqual(await page.evaluate(() => document.documentElement.style.overflow), 'hidden');
    assert.equal(await background.getAttribute('data-effect'), entry.effect.type);
    assert.equal(await background.evaluate(el => getComputedStyle(el).pointerEvents), 'none');
    assert.equal(await background.evaluate(el => getComputedStyle(el).zIndex), '-1');
    if (entry.effect.assets.includes('snowflake.svg')) {
      const visibility = await background.evaluate(el => {
        const style = getComputedStyle(el), color = getComputedStyle(document.body).backgroundColor;
        const rgb = color.match(/\d+/g).slice(0, 3).map(Number);
        function composite(filter, opacity) {
          const sample = document.createElement('canvas'); sample.width = el.width; sample.height = el.height;
          const ctx = sample.getContext('2d'); ctx.fillStyle = color; ctx.fillRect(0, 0, sample.width, sample.height);
          ctx.filter = filter; ctx.globalAlpha = opacity; ctx.drawImage(el, 0, 0);
          const pixels = ctx.getImageData(0, 0, sample.width, sample.height).data;
          let strong = 0, peak = 0;
          for (let n = 0; n < pixels.length; n += 4) {
            const delta = Math.max(...rgb.map((channel, i) => Math.abs(pixels[n + i] - channel)));
            if (delta >= 6) strong++; peak = Math.max(peak, delta);
          }
          return { strong, peak };
        }
        return { before: composite('none', .65), after: composite(style.filter, Number(style.opacity)) };
      });
      assert.ok(visibility.after.strong > visibility.before.strong && visibility.after.strong > 100, `${entry.id} needs visible snow on the light page`);
      assert.ok(visibility.after.peak > visibility.before.peak && visibility.after.peak >= 12, `${entry.id} needs perceptible contrast`);
    }
    const bounds = await background.boundingBox();
    assert.deepEqual([bounds.x, bounds.y, bounds.width, bounds.height], [0, 0, 1440, 900]);
    assert.equal(await row.getAttribute('aria-pressed'), 'true');
    assert.equal(await row.evaluate(el => getComputedStyle(el).boxShadow), 'none');
    await row.click();
    await removed(page);
    assert.equal(await background.count(), 0, `${entry.id} must be removed, not paused`);
    assert.equal(await row.getAttribute('aria-pressed'), 'false');
    assert.equal(await row.getAttribute('data-preview'), null);
    assert.deepEqual(await row.evaluate(el => ({ background: getComputedStyle(el).backgroundColor, shadow: getComputedStyle(el).boxShadow })), originalStyle);
    await row.click();
    await settled(page);
  }
  // Cancellation also applies to the empty background. A new row click restarts.
  await page.mouse.click(4, 400);
  await removed(page);
  assert.equal(await background.count(), 0);
  await page.mouse.click(4, 400);
  await removed(page);
  assert.equal(await background.count(), 0);
  await page.locator('[data-song-id] button').last().click();
  await settled(page);
  const search = page.getByRole('searchbox');
  await search.fill('Wham');
  assert.equal(await page.locator('[data-song-id]').count(), 1);
  assert.equal(await background.getAttribute('data-paused'), 'false');
  await search.fill('');
  await page.locator('[data-song-id]').first().locator('button').click();
  await settled(page);
  await page.evaluate(() => scrollTo({ top: 0, behavior: 'instant' }));
  await page.screenshot({ path: path.join(output, 'inline-background-preview.png') });
  assert.ok(await page.locator('h1').isVisible());
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(100);
  assert.deepEqual(await background.evaluate(el => [el.clientWidth, el.clientHeight]), [390, 844]);
  await page.screenshot({ path: path.join(output, 'inline-background-mobile.png') });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.waitForTimeout(150);
  const staticFrame = await background.evaluate(el => el.toDataURL());
  await page.waitForTimeout(150);
  assert.equal(await background.evaluate(el => el.toDataURL()), staticFrame);
  await page.keyboard.press('Escape');
  await removed(page);
  assert.equal(await background.count(), 0);
  await page.route('**/images/song-effects/**', route => route.abort());
  await page.locator('[data-song-id] button').first().click();
  await page.getByText('彩蛋加载失败，请重试。', { exact: true }).waitFor();
  await page.unroute('**/images/song-effects/**');
  await page.locator('[data-song-id] button').first().click();
  await settled(page);
  for (const prefix of ['en', 'zh-hant', 'ja']) {
    await page.goto(`${base}/${prefix}/songs/`, { waitUntil: 'networkidle' });
    await page.locator('[data-song-id] button').first().click();
    await settled(page);
    assert.equal(await page.getByRole('dialog').count(), 0);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
  }
  assert.deepEqual(errors, []);
  await browser.close();
  console.log('Passed: all 13 inline page backgrounds, unchanged URL/scroll, no modal, second-click cancellation and exact row style restoration, usable search, responsive hint, mobile size, reduced motion, Esc, retry and four locales.');
})().catch(error => { console.error(error); process.exit(1); });

const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const catalog = require('../data/song-effects-catalog.json');
const facts = require('../data/song-effect-facts.json');
const signature = entry => `${entry.effect.type}:${[...entry.effect.assets].sort().join(',')}`;
const base = process.env.BASE_URL || 'http://127.0.0.1:3218';
const output = path.resolve(__dirname, '../.screenshots-easter-songs');
const waitReady = page => page.locator('#quiz canvas[data-ready="true"]').waitFor();

(async () => {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'zh-CN' });
  const page = await context.newPage(), errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(base + '/songs/', { waitUntil: 'networkidle' });
  const rows = page.locator('[data-song-id]');
  assert.equal(await rows.count(), 13);
  assert.equal(await page.locator('canvas').count(), 0);
  for (const removed of ['我的彩蛋收集册', '让彩蛋来到网页', '复制歌名', '歌曲与歌手信息需匹配', '歌单依据 v3.7.2']) assert.ok(!(await page.locator('body').textContent()).includes(removed), `Still contains ${removed}`);
  assert.equal(await page.locator('#playlist button[aria-pressed]').count(), 13);
  assert.ok(await rows.first().evaluate(el => el.getBoundingClientRect().height <= 68));
  await page.screenshot({ path: path.join(output, 'preview-v3.png') });
  await page.screenshot({ path: path.join(output, 'desktop-v3.png'), fullPage: true });
  await page.getByRole('button', { name: '揭晓今日彩蛋', exact: true }).click();
  const firstDraw = await page.locator('#daily h3').textContent();
  await page.getByRole('button', { name: /再抽一张/ }).click();
  assert.notEqual(await page.locator('#daily h3').textContent(), firstDraw);
  assert.equal(await page.locator('#daily button').count(), 1);
  assert.equal(await page.locator('canvas').count(), 0);
  const search = page.getByRole('searchbox');
  await search.fill('Taylor Swift'); assert.equal(await rows.count(), 4);
  await search.fill('Wham'); assert.equal(await rows.count(), 1);
  await search.fill('不存在'); await page.getByRole('heading', { name: '还没找到这首歌' }).waitFor();
  await page.getByRole('button', { name: '清空筛选', exact: true }).click();
  for (const entry of catalog.entries) assert.ok((await page.locator(`[data-song-id="${entry.id}"]`).textContent()).includes(facts[signature(entry)][0]));

  // Fix only the next draw to cover every real effect. Restore Math.random
  // immediately so the production renderer and option shuffles stay unmodified.
  let previous;
  for (let i = 0; i < catalog.entries.length; i++) {
    const entry = catalog.entries[i];
    const pool = catalog.entries.filter(song => song.id !== previous);
    const value = (pool.findIndex(song => song.id === entry.id) + .1) / pool.length;
    await page.evaluate(value => { const original = Math.random; Math.random = () => { Math.random = original; return value; }; }, value);
    await page.getByRole('button', { name: i ? /下一题/ : /开始猜歌/ }).click();
    await waitReady(page);
    const canvas = page.locator('#quiz canvas');
    const effect = facts[signature(entry)][0];
    assert.ok((await canvas.getAttribute('aria-label')).includes(effect));
    assert.equal(await canvas.getAttribute('data-effect'), entry.effect.type);
    assert.equal(await page.locator('canvas').count(), 1);
    const bounds = await canvas.boundingBox();
    assert.ok(bounds.height <= 245 && bounds.width < 700, 'Scene must stay inside the question');
    assert.equal(await canvas.evaluate(el => getComputedStyle(el).position), 'static');
    await page.waitForTimeout(150);
    assert.ok(await canvas.evaluate(el => {
      const pixels = el.getContext('2d').getImageData(0, 0, el.width, el.height).data;
      for (let n = 3; n < pixels.length; n += 4) if (pixels[n]) return true;
      return false;
    }), `No visible pixels for ${entry.id}`);
    await page.locator('[class*="visualClue"]').screenshot({ path: path.join(output, `clue-${entry.id}.png`) });
    // The description is accessible, but sighted players get the actual visual
    // instead of a text-only clue before answering.
    assert.equal(await page.locator('[class*="clueFallback"]').count(), 0);
    assert.ok(!(await page.locator('[class*="visualClue"] figcaption').textContent()).includes(effect));
    const options = page.locator('[class*="quizOptions"] button');
    const titles = await options.locator('strong').allTextContents();
    const valid = titles.filter(title => catalog.entries.some(song => song.match.titleBase === title && facts[signature(song)][0] === effect));
    assert.equal(valid.length, 1);
    const correctIndex = titles.indexOf(valid[0]);
    await options.nth(i % 2 ? (correctIndex + 1) % 3 : correctIndex).click();
    assert.equal(await page.locator('button[data-answer="correct"]').count(), 1);
    assert.ok(await options.first().isDisabled());
    await page.locator('#quiz canvas[data-paused="true"]').waitFor();
    previous = entry.id;
  }
  await page.getByRole('button', { name: /再看一次/ }).click();
  await waitReady(page);
  await page.locator('#quiz canvas[data-paused="false"]').waitFor();
  await page.waitForTimeout(5200);
  assert.equal(await page.locator('canvas').getAttribute('data-paused'), 'true');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.waitForTimeout(150);
  const still = await page.locator('canvas').evaluate(el => el.toDataURL());
  await page.waitForTimeout(150);
  assert.equal(await page.locator('canvas').evaluate(el => el.toDataURL()), still);
  await page.locator('#quiz').screenshot({ path: path.join(output, 'quiz-v3.png') });
  for (const width of [1440, 1024, 768, 390, 320]) {
    await page.setViewportSize({ width, height: 900 }); await page.waitForTimeout(100);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Overflow at ${width}`);
    assert.ok(await rows.first().evaluate(el => el.getBoundingClientRect().height <= 80));
    await page.evaluate(() => { document.activeElement?.blur(); scrollTo({ top: 0, behavior: 'instant' }); });
    await page.waitForTimeout(100);
    await page.screenshot({ path: path.join(output, `v3-${width}.png`), fullPage: true });
  }
  for (const [prefix, lang] of [['/en', 'en'], ['/zh-hant', 'zh-Hant'], ['/ja', 'ja']]) {
    await page.goto(base + prefix + '/songs/', { waitUntil: 'networkidle' });
    assert.equal(await page.locator('html').getAttribute('lang'), lang);
    assert.equal(await rows.count(), 13);
    assert.equal(await page.locator('canvas').count(), 0);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Overflow in ${lang}`);
  }
  // Optional browser storage is no longer part of the page at all.
  const noStorage = await browser.newContext({ locale: 'zh-CN' });
  await noStorage.addInitScript(() => Object.defineProperty(window, 'localStorage', { get() { throw new Error('Storage blocked'); } }));
  const fallback = await noStorage.newPage();
  await fallback.goto(base + '/songs/', { waitUntil: 'networkidle' });
  await fallback.route('**/images/song-effects/**', route => route.abort());
  await fallback.getByRole('button', { name: /开始猜歌/ }).click();
  await fallback.getByText('动画暂时无法加载，可以重试或参考下方线索。', { exact: true }).waitFor();
  assert.ok(await fallback.locator('[class*="clueFallback"]').isVisible());
  await fallback.unroute('**/images/song-effects/**');
  await fallback.getByRole('button', { name: /重新加载/ }).click();
  await waitReady(fallback);
  assert.deepEqual(errors, []);
  await browser.close();
  console.log('Passed: requested removals, compact rows, search, draw, all 13 visual clues, unique quiz answers, feedback, replay/automatic stop, reduced motion, four languages, responsive layouts, asset retry, blocked storage.');
})().catch(error => { console.error(error); process.exit(1); });

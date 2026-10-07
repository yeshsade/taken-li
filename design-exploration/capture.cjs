// Render the design exploration, not the Windows application.
const { chromium } = require('/opt/codex/runtimes/cua/lib/node_modules/playwright-core');
const path = require('node:path');
const fs = require('node:fs');
const filename = process.argv[2] || 'settings.html';
const prefix = process.argv[3] || 'direction';
(async () => {
  const browser = await chromium.launch({ executablePath: '/usr/bin/chromium', headless: true, timeout: 20000, args: ['--no-sandbox', '--disable-dev-shm-usage', '--disable-gpu'] });
  try {
    const errors = [];
    for (const lang of ['he', 'en']) {
      for (const mode of ['1', '2', '3']) {
        const page = await browser.newPage({ viewport: { width: 1060, height: 800 } });
        page.on('pageerror', error => errors.push(error.message));
        const markup = fs.readFileSync(path.join(__dirname, filename), 'utf8').replace('const query=new URLSearchParams(location.search);', `const query=new URLSearchParams('mode=${mode}&lang=${lang}');`);
        await page.setContent(markup, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await page.locator('.action').last().waitFor();
        const result = await page.evaluate(() => ({ direction: document.documentElement.dir, keys: [...document.querySelectorAll('.key')].map(x => x.textContent), overflowing: [...document.querySelectorAll('.window, .action, .pane')].some(x => x.scrollWidth > x.clientWidth + 1) }));
        if (result.direction !== (lang === 'he' ? 'rtl' : 'ltr') || result.keys.join('|') !== 'F10|Shift+F10|F6' || result.overflowing) throw new Error(JSON.stringify({ lang, mode, result }));
        await page.screenshot({ path: path.join(__dirname, `${prefix}-${mode}${lang === 'en' ? '-en' : ''}.png`), fullPage: true, timeout: 15000 });
        console.log(JSON.stringify({ lang, mode, ...result }));
        if (filename === 'modern-settings.html') {
          const contrast = await page.evaluate(() => {
            const style = getComputedStyle(document.documentElement);
            const themed = getComputedStyle(document.body);
            const token = name => themed.getPropertyValue(name).trim() || style.getPropertyValue(name).trim();
            const luminance = hex => {
              const expanded = hex.length === 4 ? '#' + [...hex.slice(1)].map(c => c + c).join('') : hex;
              const rgb = [1, 3, 5].map(i => parseInt(expanded.slice(i, i + 2), 16) / 255).map(c => c <= .04045 ? c / 12.92 : ((c + .055) / 1.055) ** 2.4);
              return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722;
            };
            return [['--text', '--card'], ['--muted', '--card'], ['--muted', '--surface'], ['--on-accent', '--accent'], ['--selected-text', '--selected']].map(([fg, bg]) => {
              const a = luminance(token(fg)), b = luminance(token(bg));
              return { pair: [fg, bg], ratio: (Math.max(a, b) + .05) / (Math.min(a, b) + .05) };
            });
          });
          if (contrast.some(pair => !Number.isFinite(pair.ratio) || pair.ratio < 4.5)) throw new Error(JSON.stringify({ mode, contrast }));
          await page.locator('#auto-switch').click();
          await page.locator('.nav-button[data-page="1"]').click();
          if (!(await page.locator('#delay').isDisabled())) throw new Error('Disabled timer remains editable');
          await page.locator('#timer-enabled').check();
          if (await page.locator('#delay').isDisabled()) throw new Error('Enabled timer is not editable');
          await page.locator('.nav-button[data-page="2"]').click();
          await page.locator('input[value="field"]').check();
          await page.locator('.nav-button[data-page="3"]').click();
          await page.locator('#startup').check();
          await page.locator('[data-cancel]').click();
          await page.locator('[data-edit="0"]').click();
          await page.locator('#key-input').press('F8');
          await page.locator('#key-ok').click();
          if (!(await page.locator('.key').first().innerText()).includes('F8')) throw new Error('Key change did not update');
          await page.locator('[data-cancel]').click();
          if ((await page.locator('.key').first().innerText()).trim() !== 'F10') throw new Error('Cancel did not restore initial keys');
          for (const width of [768, 375]) {
            await page.setViewportSize({ width, height: 900 });
            if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) throw new Error(`Horizontal overflow: ${lang} mode ${mode} width ${width}`);
          }
          console.log(JSON.stringify({ lang, mode, minimumContrast: Math.min(...contrast.map(x => x.ratio)).toFixed(2), navigationAndKeyCapture: 'passed', narrowLayouts: 'passed' }));
        }
        await page.close();
      }
    }
    if (errors.length) throw new Error(errors.join('\n'));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

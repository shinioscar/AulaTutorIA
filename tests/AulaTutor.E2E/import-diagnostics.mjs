// Focused import QA. Supply rejected/accepted JSON paths privately; no user documents in Git.
import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.AULATUTOR_PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(import.meta.dirname, '../..');
const qa = JSON.parse(await fs.readFile(path.join(root, '.cache/http-qa.json'), 'utf8'));
assert.match(qa.database, /^aulatutor_http_[a-f0-9]{32}$/);
if (process.argv.length !== 4) throw Error('Supply rejected and accepted JSON paths. No provider is called.');
const rejected = await fs.readFile(process.argv[2], 'utf8'), accepted = await fs.readFile(process.argv[3], 'utf8');
const origin = 'https://localhost:4200';
const browser = await chromium.launch({ headless: true, executablePath: process.env.AULATUTOR_EDGE_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } });
const page = await context.newPage(); page.setDefaultTimeout(20000);
let javascriptErrors = 0, generationRequests = 0;
const fits = [];
page.on('pageerror', () => javascriptErrors++);
await page.route('**/*', route => {
  const url = new URL(route.request().url());
  if (url.hostname !== 'localhost') return route.abort();
  if (route.request().method() === 'POST' && /\/(ai|messages|test|design)(\/|$)/.test(url.pathname)) { generationRequests++; return route.abort(); }
  return route.continue();
});
async function validate(json) {
  await page.getByLabel('Clase generada', { exact: true }).fill(json);
  const [response] = await Promise.all([page.waitForResponse(r => r.url().endsWith('/api/lessons/import/preview')), page.getByRole('button', { name: 'Validar y revisar', exact: true }).click()]);
  assert.equal(response.status(), 200); return response.json();
}
async function captures(state, target) {
  for (const width of [320, 390, 1440]) for (const dark of [false, true]) {
    await page.setViewportSize({ width, height: width === 1440 ? 1000 : 900 });
    const toggle = page.getByRole('button', { name: 'Tema oscuro', exact: true });
    if ((await toggle.getAttribute('aria-pressed') === 'true') !== dark) await toggle.click();
    await target.scrollIntoViewIfNeeded();
    const fit = await page.evaluate(() => ({ width: innerWidth, scroll: document.documentElement.scrollWidth, theme: document.documentElement.dataset.theme }));
    assert.ok(fit.scroll <= fit.width + 1); fits.push({ state, ...fit });
    await page.screenshot({ path: path.join(root, `artifacts/import-${state}-${width}-${dark ? 'dark' : 'light'}.png`), animations: 'disabled' });
  }
}
try {
  assert.equal((await (await context.request.get(origin + '/api/health')).json()).aiProvider, 'Mock');
  await page.goto(origin + '/login'); await page.getByLabel('Correo electrónico').fill(qa.teacherEmail);
  await page.getByLabel('Contraseña', { exact: true }).fill(qa.password);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click(); await page.waitForURL(origin + '/');
  const before = await (await context.request.get(origin + '/api/lessons/')).json();
  await page.goto(origin + '/teacher/lessons/new/ai');
  await page.getByLabel('Clase precargada:', { exact: false }).check();
  await page.getByLabel('Tema de la clase', { exact: true }).fill('Bases de datos ficticias');
  await page.getByLabel('Cantidad aproximada de ejercicios', { exact: true }).fill('4');
  await page.getByRole('button', { name: 'Usar ChatGPT', exact: true }).click();
  const prompt = await page.getByLabel('Instrucciones para la herramienta externa', { exact: true }).inputValue();
  assert.ok(prompt.includes('hintRules 600') && prompt.includes('escapes Unicode'));
  const invalid = await validate(rejected);
  assert.equal(invalid.content, null); assert.equal(invalid.errors.length, 1);
  assert.ok(invalid.errors[0].includes('20038') && invalid.errors[0].includes('exceso: 38'));
  await page.getByRole('alert').filter({ hasText: '20038' }).waitFor();
  await captures('rejected', page.getByRole('alert').filter({ hasText: '20038' }));
  await page.getByText('Seleccionar instrucciones para copiar manualmente', { exact: true }).click();
  const correction = await page.getByLabel('Instrucciones de corrección', { exact: true }).inputValue();
  assert.ok(correction.includes(invalid.errors[0]) && correction.includes('aulatutor-lesson-v2'));
  // HTML textarea normalizes CRLF to LF; validation must preserve the text otherwise.
  assert.ok((await page.getByLabel('Clase generada', { exact: true }).inputValue()) === rejected.replace(/\r\n?/g, '\n'));
  const valid = await validate(accepted); assert.deepEqual(valid.errors, []); assert.ok(valid.content?.preloaded);
  const save = page.getByRole('button', { name: 'Guardar borrador y abrir constructor', exact: true });
  await save.waitFor(); await captures('accepted', save);
  const after = await (await context.request.get(origin + '/api/lessons/')).json(); assert.equal(after.length, before.length);
  assert.equal(javascriptErrors, 0); assert.equal(generationRequests, 0);
  await fs.writeFile(path.join(root, 'artifacts/import-diagnostics-e2e.json'), JSON.stringify({ status: 'passed', checks: ['shared-prompt-limits', 'exact-size-rejection', 'safe-correction', 'input-preserved', 'corrected-preview', 'no-persistence'], fits, javascriptErrors, generationRequests, scope: 'Actual HTTPS UI, isolated PostgreSQL, Mock only; no save, approval, publication or personal browser session.' }, null, 2));
  console.log('Import diagnostics passed: six functional assertions and twelve Light/Dark viewport captures. No writes or provider calls.');
} finally { await context.clearCookies(); await browser.close(); }

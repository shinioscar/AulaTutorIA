// Existing local Edge + supplied Playwright module. No browser/package download.
import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { academicPilot } from './academic-pilot.mjs';
const suiteStarted=performance.now();
const requestMetrics=[];
const root = path.resolve(import.meta.dirname, '../..');
const temporary = path.join(root, '.cache/e2e-temp'); await fs.mkdir(temporary, { recursive: true });
process.env.TEMP = temporary; process.env.TMP = temporary;
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.AULATUTOR_PLAYWRIGHT_MODULE || 'playwright');
const qa = JSON.parse(await fs.readFile(path.join(root, '.cache/http-qa.json'), 'utf8'));
assert.match(qa.database, /^aulatutor_http_[a-f0-9]{32}$/);
const origin = 'https://localhost:4200';
const evidence = path.join(root, 'docs/iterations/evidence-9'); await fs.mkdir(evidence, { recursive: true });
const results = []; const pageErrors = []; let step = 'startup'; let browser, lastPage;
await fs.writeFile(path.join(root, 'artifacts/iteration-9-e2e.json'), JSON.stringify({ status: 'running', checks: [] }));
const generationTimes = new WeakMap();
async function rateLimit(page) {
  const times = (generationTimes.get(page) || []).filter(t => Date.now() - t < 61000);
  if (times.length >= 5) await new Promise(resolve => setTimeout(resolve, Math.max(0, times[0] + 61000 - Date.now())));
  generationTimes.set(page, [...times.filter(t => Date.now() - t < 61000), Date.now()]);
}
async function check(name, action) { step = name; console.log('E2E: ' + name); const started=performance.now();await action(); results.push({ name, passed: true, elapsedMs:performance.now()-started }); }
function metric(method,url,status,started){requestMetrics.push({method,route:new URL(url,origin).pathname.replace(/[a-f0-9-]{36}/g,'{id}'),status,elapsedMs:performance.now()-started});}
function measuredRequests(){const groups=new Map();for(const r of requestMetrics){const key=r.method+' '+r.route;groups.set(key,[...(groups.get(key)||[]),r]);}return [...groups].map(([route,rows])=>{const times=rows.map(r=>r.elapsedMs).sort((a,b)=>a-b),p=q=>times[Math.ceil(q*times.length)-1];return {route,n:rows.length,success:rows.filter(r=>r.status<400).length,errors:rows.filter(r=>r.status>=400).length,p50Ms:rows.length>=20?p(.5):null,p95Ms:rows.length>=20?p(.95):null,minMs:times[0],maxMs:times.at(-1)};});}
async function login(email) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage(); page.setDefaultTimeout(20000); page.on('dialog', d => d.accept());
  page.on('pageerror', () => pageErrors.push('runtime error'));
  await page.route('**/*', route => new URL(route.request().url()).hostname === 'localhost' ? route.continue() : route.abort());
  await page.goto(origin + '/login'); await page.getByLabel('Correo electrónico').fill(email); await page.getByLabel('Contraseña', { exact: true }).fill(qa.password);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click(); await page.waitForURL(origin + '/');
  return { context, page };
}
async function api(context, method, url, data) {
  const csrf = await (await context.request.get(origin + '/api/auth/csrf')).json();
  const started=performance.now();
  const response = await context.request.fetch(origin + url, { method, data, headers: { 'X-XSRF-TOKEN': csrf.requestToken } });
  metric(method,url,response.status(),started);
  assert.ok(response.ok(), `${method} ${url.replace(/[a-f0-9-]{36}/g,'{id}')} status ${response.status()}`);
  const body=await response.text();return body?JSON.parse(body):null;
}
async function mutation(page, name, pattern) {
  lastPage = page;
  if (pattern.test('/messages')) await rateLimit(page);
  const started=performance.now();
  const [r] = await Promise.all([page.waitForResponse(r => r.request().method() !== 'GET' && pattern.test(r.url())),
    page.getByRole('button', { name, exact: true }).click()]);
  metric(r.request().method(),r.url(),r.status(),started);assert.ok(r.ok(), `${name}: ${r.status()}`); return r.json().catch(() => null);
}
async function capture(page, name, width = 1440, dark = false, focus = null) {
  await page.setViewportSize({ width, height: width < 600 ? 900 : 1000 });
  // Exercise the actual theme control and its persisted preference.
  const toggle = page.getByRole('button', { name: 'Tema oscuro', exact: true });
  if ((await toggle.getAttribute('aria-pressed') === 'true') !== dark) await toggle.click();
  await page.waitForFunction(theme => document.documentElement.dataset.theme === theme, dark ? 'dark' : 'light');
  if (focus) await focus.scrollIntoViewIfNeeded();
  const fit = await page.evaluate(() => ({ width: innerWidth, scroll: document.documentElement.scrollWidth, theme: document.documentElement.dataset.theme }));
  assert.ok(fit.scroll <= fit.width + 1); results.push({ name: 'viewport-' + name, passed: true, ...fit });
  await page.screenshot({ path: path.join(evidence, name + '.png'), fullPage: false, animations: 'disabled' });
}
try {
  browser = await chromium.launch({ headless: true, executablePath: process.env.AULATUTOR_EDGE_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const teacher = await login(qa.teacherEmail), student = await login(qa.studentEmail), admin = await login(qa.adminEmail);
  const health = await (await teacher.context.request.get(origin + '/api/health')).json(); assert.equal(health.aiProvider, 'Mock');
  // Let the retained HTTP harness's process rate windows expire; never change server limits for QA.
  await new Promise(resolve => setTimeout(resolve, 61000));
  let lessonId, assignment, sessionId, groupId, secondTeacher;
  await check('A: browser registers teacher, admin approves and creates authorized group', async () => {
    const email = 'e2e-' + crypto.randomUUID() + '@example.invalid'; const display = 'Docente E2E ' + crypto.randomUUID().slice(0,8);
    const registration = await browser.newContext({ ignoreHTTPSErrors: true }); const p = await registration.newPage();
    p.on('pageerror', () => pageErrors.push('runtime error'));
    await p.route('**/*', route => new URL(route.request().url()).hostname === 'localhost' ? route.continue() : route.abort());
    await p.goto(origin + '/register'); await p.getByLabel('Nombre visible').fill(display); await p.getByLabel('Correo electrónico').fill(email);
    await p.getByLabel('Contraseña', { exact: true }).fill(qa.password); await p.getByLabel('Tipo de cuenta').selectOption('Teacher');
    await mutation(p, 'Enviar registro', /\/auth\/register$/); await registration.close();
    const pending = await login(email);
    assert.equal((await pending.context.request.get(origin + '/api/groups/')).status(), 403);
    await admin.page.goto(origin + '/admin/users'); await mutation(admin.page, 'Aprobar a ' + display, /\/teacher-decision$/);
    assert.equal((await pending.context.request.get(origin + '/api/me')).status(), 401); await pending.context.close();
    secondTeacher = await login(email);
    const groupName = 'Grupo E2E ' + crypto.randomUUID().slice(0,8); await admin.page.goto(origin + '/groups');
    await admin.page.getByLabel('Nombre del grupo', { exact: true }).fill(groupName); const group = await mutation(admin.page, 'Crear grupo', /\/api\/groups\/?$/); groupId = group.id;
    await admin.page.getByRole('button', { name: new RegExp('^' + groupName) }).click();
    for (const [actor, role] of [[teacher, 'Teacher'], [secondTeacher, 'Teacher'], [student, 'Student']]) {
      const user = await api(actor.context, 'GET', '/api/me');
      await admin.page.getByLabel('Usuario de la organización').selectOption(user.id); await admin.page.getByLabel('Relación con el grupo').selectOption(role);
      await Promise.all([admin.page.waitForResponse(r => r.request().method() === 'GET' && new URL(r.url()).pathname === '/api/admin/users'),
        mutation(admin.page, 'Asociar al grupo', /\/members$/)]);
      await admin.page.waitForFunction(() => document.querySelector('#member')?.value === '');
    }
  });
  await check('UI-1: existing lesson and immutable versions remain readable', async () => {
    const items=await api(teacher.context,'GET','/api/lessons/?page=1');let legacy;
    for(const item of items){const view=await api(teacher.context,'GET','/api/lessons/'+item.id);if(view.versions.length>=2){legacy=view;break;}}
    assert.ok(legacy,'HTTPS fixture must contain existing published versions');
    await teacher.page.goto(origin+'/teacher/lessons/'+legacy.id);
    await teacher.page.getByRole('button',{name:'3. Revisar y publicar',exact:true}).click();
    await teacher.page.getByRole('button',{name:'Ver versión 1',exact:true}).click();
    await teacher.page.getByRole('heading',{name:'Contenido congelado · versión 1',exact:true}).waitFor();
    await capture(teacher.page,'existing-versions-light',1440,false,teacher.page.getByRole('heading',{name:'Versiones inmutables',exact:true}));
    const after=await api(teacher.context,'GET','/api/lessons/'+legacy.id);assert.deepEqual(after.versions,legacy.versions);
  });
  await check('B: browser creates, designs, applies, approves and publishes a lesson', async () => {
    const p = teacher.page; await p.goto(origin + '/teacher/lessons');
    await p.getByRole('heading',{name:'Mis clases',exact:true}).waitFor();
    await p.locator('.lesson-grid[aria-busy="false"]').waitFor();
    await capture(p, 'library-light', 1440, false); await capture(p, 'library-dark-320', 320, true);
    await p.getByRole('link', { name: /Crear clase manualmente/ }).click();
    await p.getByRole('heading',{name:'Comienza con una idea',exact:true}).waitFor();
    await capture(p, 'manual-light-390', 390, false);
    await p.getByLabel('Título', { exact: true }).fill('Borrador manual UI-1'); await p.getByRole('button', { name: 'Crear borrador', exact: true }).click();
    await p.waitForURL(/\/teacher\/lessons\/[a-f0-9-]{36}$/);
    await p.getByLabel('Descripción', {exact:true}).fill('Descripción ficticia conservada');
    await mutation(p, 'Guardar borrador', /\/draft$/); await p.reload();
    assert.equal(await p.getByLabel('Descripción', {exact:true}).inputValue(), 'Descripción ficticia conservada');
    await p.getByRole('button', {name:'2. Contenido',exact:true}).click();
    await p.getByRole('button', {name:'Añadir objetivo',exact:true}).click();
    await p.locator('textarea').first().fill('Objetivo manual ficticio');
    await p.getByRole('button', {name:'Añadir sección',exact:true}).click();
    await p.getByLabel('Título de sección',{exact:true}).fill('Sección manual');
    await p.getByLabel('Contenido',{exact:true}).fill('Contenido manual ficticio');
    await p.getByRole('button',{name:'Añadir actividad a sección 1',exact:true}).click();
    await p.locator('.exercise-editor > summary').click();
    await p.getByLabel('Tipo',{exact:true}).selectOption('ShortAnswer');
    await p.getByLabel('Consigna',{exact:true}).fill('Escribe la respuesta ficticia');
    await p.getByLabel('Alternativas aceptadas privadas (una por línea)',{exact:true}).fill('Respuesta ficticia');
    await p.getByText('Rúbrica, solución y pistas',{exact:true}).click();
    await p.getByLabel('Reglas para ofrecer pistas',{exact:true}).fill('Pista ficticia privada');
    await mutation(p,'Guardar borrador', /\/draft$/);
    await capture(p,'manual-content-dark',1440,true);
    await p.getByRole('link',{name:'← Mis clases',exact:true}).click();
    await p.getByLabel('Buscar en esta página').fill('Borrador manual UI-1');
    await p.getByRole('link',{name:'Borrador manual UI-1',exact:true}).waitFor();
    await p.waitForFunction(()=>document.querySelectorAll('.lesson-card').length===1);
    assert.equal(await p.locator('.lesson-card').count(),1);
    await p.getByLabel('Estado',{exact:true}).selectOption('Published');
    await p.getByRole('heading',{name:'No hay coincidencias',exact:true}).waitFor();
    await p.getByRole('button',{name:'Limpiar filtros',exact:true}).click();
    await p.getByRole('link',{name:/Crear con apoyo de IA/}).click();
    await p.getByLabel('Tema de la clase',{exact:true}).fill('Borrador recuperable UI-1');
    await p.getByLabel('Indicaciones adicionales (opcional)',{exact:true}).fill('Conservar estas indicaciones ficticias');
    await p.route('**/api/lessons/*/design',route=>route.fulfill({status:429,contentType:'application/problem+json',body:JSON.stringify({title:'Cuota simulada UI-1'})}),{times:1});
    await p.getByRole('button',{name:'Generar con IA integrada',exact:true}).click();
    await p.waitForURL(/generation=check/);
    await p.getByText('La generación no se confirmó.',{exact:false}).waitFor();
    assert.ok((await p.getByLabel('Petición al asistente').inputValue()).includes('Conservar estas indicaciones ficticias'));
    await capture(p,'ai-recovery-dark-390',390,true);
    await p.getByRole('link',{name:'← Mis clases',exact:true}).click();
    await p.getByRole('link',{name:/Crear con apoyo de IA/}).click();
    await p.getByRole('heading',{name:'Prepara tu clase con IA',exact:true}).waitFor();
    await capture(p,'ai-light',1440,false); await capture(p,'ai-dark-390',390,true);
    await p.getByLabel('Tema de la clase',{exact:true}).fill('Clase E2E UI-1');
    await p.getByLabel('Indicaciones adicionales (opcional)',{exact:true}).fill('SQL ficticio para comprobar el flujo.');
    await p.getByRole('button',{name:'Generar con IA integrada',exact:true}).click();
    await p.waitForURL(/\/teacher\/lessons\/[a-f0-9-]{36}\?mode=ai$/);
    lessonId=new URL(p.url()).pathname.split('/').at(-1);
    await mutation(p,'Descartar propuesta',/\/discard-proposal$/);
    await p.getByLabel('Petición al asistente').fill('Propón una clase ficticia de SQL con dos actividades para comprobar el flujo.');
    await mutation(p, 'Solicitar propuesta', /\/design$/); await mutation(p, 'Aplicar propuesta', /\/apply-proposal$/);
    await p.getByRole('button',{name:'1. Información básica',exact:true}).click();
    await p.getByLabel('Título',{exact:true}).fill('Clase E2E UI-1 revisada');
    await mutation(p,'Guardar borrador',/\/draft$/);
    await p.getByRole('button', { name: '3. Revisar y publicar', exact: true }).click();
    await mutation(p,'Simular como estudiante',/\/simulations$/);
    await p.waitForURL(/\/simulations\//);
    await mutation(p,'Comenzar enseñanza simulada',/\/messages$/);
    await mutation(p,'Detener simulación',/\/stop$/);
    await p.getByRole('link',{name:'← Volver al editor',exact:true}).click();
    await p.waitForURL(/\/teacher\/lessons\/[a-f0-9-]{36}$/); lessonId = p.url().split('/').at(-1);
    await p.getByRole('button', { name: '3. Revisar y publicar', exact: true }).click();
    await mutation(p, 'Pasar a revisión', /\/review$/); await mutation(p, 'Confirmar aprobación de esta revisión', /\/approve$/);
    await mutation(p, 'Confirmar publicación de versión 1', /\/publish$/); await capture(p, 'teacher-published-light', 1440, false, p.getByRole('heading', { name: 'Versiones inmutables', exact: true }));
  });
  await check('IA-1: shared options, copy, safe correction, preview, imported edit/simulation/publication', async () => {
    const p=teacher.page;
    const previous=await api(teacher.context,'GET','/api/lessons/'+lessonId);
    const c=previous.content;
    const wire={format:'aulatutor-lesson-v1',lesson:{explanation:'Ejemplo ficticio importado para QA',
      title:'Clase importada IA-1',subject:c.subject,description:c.description,prerequisites:c.prerequisites,
      level:c.level,durationMinutes:c.durationMinutes,methodology:c.methodology,instructions:c.instructions,
      objectives:c.objectives,sections:c.sections.map(s=>({id:s.id,title:s.title,body:s.content})),
      exercises:c.sections.flatMap(s=>s.exercises.map(e=>({id:e.id,sectionId:s.id,objectiveId:e.objectiveId,
        required:e.required,kind:e.activity.kind,prompt:e.activity.prompt,options:e.activity.options,
        acceptedAnswers:e.activity.acceptedAnswers,publicRubric:e.activity.rubric,criteria:e.rubric.criteria,
        referenceSolution:e.rubric.referenceSolution||'Referencia privada ficticia IA-1',hintRules:e.rubric.hintRules})))}};
    await p.goto(origin+'/teacher/lessons/new/ai');
    // Use the same UI-1 form and retain values on the return journey.
    if(await p.getByRole('heading',{name:'Prepara tu clase con IA',exact:true}).count()===0){
      await p.goto(origin+'/teacher/lessons');await p.getByRole('link',{name:/Crear con apoyo de IA/}).click();
    }
    await p.getByLabel('Tema de la clase',{exact:true}).fill('Fracciones ficticias IA-1');
    await p.getByRole('button',{name:'Usar ChatGPT',exact:true}).click();
    await p.getByRole('heading',{name:'Crear con ChatGPT',exact:true}).waitFor();
    const prompt=await p.getByLabel('Instrucciones para la herramienta externa').inputValue();
    assert.ok(prompt.includes('Fracciones ficticias IA-1'));assert.ok(prompt.includes('aulatutor-lesson-v1'));
    assert.equal(await p.getByRole('link',{name:'Abrir ChatGPT ↗',exact:true}).getAttribute('href'),'https://chatgpt.com/');
    await teacher.context.grantPermissions(['clipboard-read','clipboard-write']);
    await p.getByRole('button',{name:'Copiar prompt',exact:true}).click();
    // Windows clipboard uses CRLF; textarea values use LF. Preserve all content.
    assert.equal((await p.evaluate(()=>navigator.clipboard.readText())).replace(/\r\n/g,'\n'),prompt.replace(/\r\n/g,'\n'));
    for(const width of [320,390,1440])for(const dark of [false,true])await capture(p,`external-${width}-${dark?'dark':'light'}`,width,dark);
    await p.getByRole('button',{name:'Regresar a editar opciones',exact:true}).click();
    assert.equal(await p.getByLabel('Tema de la clase',{exact:true}).inputValue(),'Fracciones ficticias IA-1');
    await p.getByRole('button',{name:'Usar ChatGPT',exact:true}).click();
    await p.getByLabel('Clase generada',{exact:true}).fill('```json\n{}');
    await mutation(p,'Validar y revisar',/\/import\/preview$/);
    await p.getByText('No se pudo reconocer el formato de la clase.',{exact:false}).waitFor();
    await p.getByRole('button',{name:'Copiar instrucciones de corrección',exact:true}).click();
    assert.ok((await p.evaluate(()=>navigator.clipboard.readText())).includes('conservando'));
    await capture(p,'import-correction-dark-390',390,true);
    await p.getByLabel('O selecciona un archivo .json (máximo 128 KiB)',{exact:true}).setInputFiles({name:'clase-ficticia.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(wire))});
    await p.waitForFunction(()=>document.querySelector('#external-json')?.value.includes('Clase importada IA-1'));
    await mutation(p,'Validar y revisar',/\/import\/preview$/);
    await p.getByRole('heading',{name:'3. Revisar la clase',exact:true}).waitFor();
    await capture(p,'import-preview-light-1440',1440,false,p.getByRole('heading',{name:'3. Revisar la clase',exact:true}));
    await p.route('**/api/lessons/import',async route=>{const response=await route.fetch();assert.equal(response.status(),201);await route.abort('failed');},{times:1});
    await p.getByRole('button',{name:'Guardar borrador y abrir constructor',exact:true}).click();
    await p.getByText('No se confirmó el guardado.',{exact:false}).waitFor();
    await mutation(p,'Guardar borrador y abrir constructor',/\/api\/lessons\/import$/);
    await p.waitForURL(/\/teacher\/lessons\/[a-f0-9-]{36}$/);lessonId=p.url().split('/').at(-1);
    assert.equal((await api(teacher.context,'GET','/api/lessons/?page=1')).filter(l=>l.title==='Clase importada IA-1').length,1);
    await p.getByLabel('Título',{exact:true}).fill('Clase importada IA-1 revisada');await mutation(p,'Guardar borrador',/\/draft$/);
    await p.getByRole('button',{name:'3. Revisar y publicar',exact:true}).click();
    await mutation(p,'Simular como estudiante',/\/simulations$/);await p.waitForURL(/\/simulations\//);
    await mutation(p,'Comenzar enseñanza simulada',/\/messages$/);await mutation(p,'Detener simulación',/\/stop$/);
    await p.getByRole('link',{name:'← Volver al editor',exact:true}).click();
    await p.getByRole('button',{name:'3. Revisar y publicar',exact:true}).click();
    await mutation(p,'Pasar a revisión',/\/review$/);await mutation(p,'Confirmar aprobación de esta revisión',/\/approve$/);
    await mutation(p,'Confirmar publicación de versión 1',/\/publish$/);
    assert.deepEqual((await api(teacher.context,'GET','/api/lessons/'+previous.id)).versions,previous.versions);
    for(const actor of [student,secondTeacher]){
      assert.equal((await actor.context.request.get(origin+'/api/lessons/'+lessonId)).status(),404);
      const csrf=await (await actor.context.request.get(origin+'/api/auth/csrf')).json();
      assert.equal((await actor.context.request.post(origin+`/api/lessons/${lessonId}/publish`,{data:{requestId:crypto.randomUUID(),revision:0},headers:{'X-XSRF-TOKEN':csrf.requestToken}})).status(),404);
    }
    assert.equal((await student.context.request.post(origin+'/api/lessons/import',{data:{requestId:crypto.randomUUID(),json:JSON.stringify(wire)}})).status(),400);
    await p.evaluate(()=>navigator.clipboard.writeText(''));
  });
  await check('B: browser assigns exact published version to authorized group', async () => {
    const p = teacher.page; await p.getByRole('link', { name: 'Asignar versión 1 a un grupo', exact: true }).click();
    await p.getByLabel('Grupo autorizado').selectOption(groupId);
    assignment = await mutation(p, 'Confirmar asignación', /\/api\/teacher\/assignments\/?$/);
  });
  await check('Concurrent teachers create/read only their own authorized assignments', async () => {
    const source = await api(teacher.context, 'GET', '/api/lessons/' + lessonId);
    let other = await api(secondTeacher.context, 'POST', '/api/lessons/', { requestId: crypto.randomUUID(), content: { ...source.content, title: 'Segundo docente E2E' } });
    for (const action of ['review','approve','publish']) other = await api(secondTeacher.context, 'POST', `/api/lessons/${other.id}/${action}`, { requestId: crypto.randomUUID(), revision: other.revision, targetId: action === 'publish' ? other.versions[0].id : null });
    const [created, existing] = await Promise.all([
      api(secondTeacher.context, 'POST', '/api/teacher/assignments/', { requestId: crypto.randomUUID(), lessonVersionId: other.versions[0].id, groupId, availableFrom: null, availableUntil: null }),
      api(teacher.context, 'GET', '/api/teacher/assignments/')
    ]);
    assert.ok(existing.every(a => a.id !== created.id));
    assert.equal((await teacher.context.request.get(origin + '/api/teacher/assignments/' + created.id)).status(), 404);
    assert.equal((await secondTeacher.context.request.get(origin + '/api/teacher/assignments/' + assignment.id)).status(), 404);
  });
  await check('C: browser starts class; staged 429 preserves input; successful recovery, pause/resume and completion', async () => {
    const p = student.page; await p.goto(origin + '/classes/' + assignment.id); await p.getByRole('button', { name: 'Iniciar clase', exact: true }).click();
    await p.waitForURL(/\/tutoring\/[a-f0-9-]{36}$/); sessionId = p.url().split('/').at(-1);
    const publicSession=await api(student.context,'GET','/api/tutoring-sessions/'+sessionId);
    for(const section of (await api(teacher.context,'GET','/api/lessons/'+lessonId)).content.sections)
      for(const exercise of section.exercises)if(exercise.rubric.referenceSolution)assert.ok(!JSON.stringify(publicSession).includes(exercise.rubric.referenceSolution));
    await mutation(p, 'Comenzar conversación', /\/messages$/);
    const input = p.getByLabel('Tu intento o pregunta'); await input.fill('Pregunta ficticia conservada ante cuota');
    await p.route('**/api/tutoring-sessions/*/messages', route => route.fulfill({ status: 429, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Cuota simulada para QA' }) }), { times: 1 });
    await p.getByRole('button', { name: 'Enviar mensaje', exact: true }).click(); await p.getByText('Se alcanzó el límite de turnos.', { exact: false }).waitFor();
    assert.equal(await input.inputValue(), 'Pregunta ficticia conservada ante cuota'); await capture(p, 'student-quota-dark-mobile', 390, true);
    await capture(p, 'student-quota-input-dark-mobile', 390, true, input);
    await mutation(p, 'Enviar mensaje', /\/messages$/);
    await mutation(p, 'Necesito una pista', /\/messages$/);
    await mutation(p, 'Pausar tutoría', /\/pause$/); await p.reload(); await mutation(p, 'Reanudar tutoría', /\/resume$/);
    // Student answers through the UI using the teacher-approved fictitious test key only in this harness.
    const lesson = await api(teacher.context, 'GET', '/api/lessons/' + lessonId);
    const exercises = lesson.content.sections.flatMap(s => s.exercises);
    for (const [index, exercise] of exercises.entries()) {
      const formInput = p.locator('[name="exercise-answer"]'); await formInput.waitFor();
      const value = exercise.activity.acceptedAnswers[0] || 'Respuesta ficticia para revisión humana';
      if (await formInput.evaluate(e => e.tagName === 'SELECT')) await formInput.selectOption(value); else await formInput.fill(value);
      await mutation(p, 'Registrar respuesta', /\/messages$/);
      const next = p.getByRole('button', { name: 'Continuar clase', exact: true }); if (index < exercises.length - 1 && await next.count() && await next.isEnabled()) await mutation(p, 'Continuar clase', /\/messages$/);
    }
    const complete = p.getByRole('button', { name: 'Finalizar clase', exact: true });
    assert.ok(await complete.isEnabled()); await mutation(p, 'Finalizar clase', /\/complete$/); await capture(p, 'student-completed-dark-mobile', 390, true, p.getByRole('heading', { name: 'Resumen de tu clase', exact: true }));
  });
  await check('D: teacher reviews actual attempts and student reads own feedback', async () => {
    const user = await api(student.context, 'GET', '/api/me'); const p = teacher.page;
    await p.goto(`${origin}/teacher/supervision/${assignment.id}/students/${user.id}`);
    await p.getByLabel('Comentario para el estudiante').fill('Revisión E2E ficticia, con historial conservado.');
    await mutation(p, 'Guardar decisión docente', /\/reviews$/); await capture(p, 'teacher-review-light', 1440, false, p.getByText('Revisión E2E ficticia, con historial conservado.', { exact: true })); await capture(p, 'teacher-review-dark-320', 320, true, p.getByText('Revisión E2E ficticia, con historial conservado.', { exact: true }));
    await student.page.goto(origin + '/classes/' + assignment.id); await student.page.getByText('Revisión E2E ficticia, con historial conservado.', { exact: true }).waitFor();
    await capture(student.page, 'student-feedback-light-mobile', 390, false, student.page.getByText('Revisión E2E ficticia, con historial conservado.', { exact: true }));
    const forbidden = await admin.context.request.get(origin + '/api/tutoring-sessions/' + sessionId); assert.equal(forbidden.status(), 404);
  });
  await check('A/E: operational admin and autonomous tutoring isolation', async () => {
    const op = await api(admin.context, 'GET', '/api/admin/operations/readiness'); assert.equal(op.database, true); assert.equal(op.aiAvailabilityChecked, false);
    const owner = await login(qa.tutoringOwnerEmail);
    await owner.page.goto(origin + '/tutoring/' + qa.completedLearningId); await owner.page.getByText('Historial privado:', { exact: false }).waitFor();
    assert.equal((await teacher.context.request.get(origin + '/api/tutoring-sessions/' + qa.completedLearningId)).status(), 404);
    assert.equal((await admin.context.request.get(origin + '/api/tutoring-sessions/' + qa.completedLearningId)).status(), 404);
    await owner.context.close();
  });
  await check('F: lost HTTP response recovery reuses request and preserves evidence', async () => {
    const session = await api(student.context, 'POST', '/api/tutoring-sessions/', { requestId: crypto.randomUUID(), topic: 'Recuperación ficticia', goal: 'Conservar el turno confirmado', level: 'Beginner', directReview: true });
    const p = student.page; await p.goto(origin + '/tutoring/' + session.id);
    await rateLimit(p);
    await p.route('**/api/tutoring-sessions/*/messages', async route => { const response = await route.fetch(); assert.ok(response.ok()); await route.abort('failed'); }, { times: 1 });
    await p.getByRole('button', { name: 'Comenzar conversación', exact: true }).click();
    await p.getByRole('button', { name: 'Actualizar estado', exact: true }).click();
    await p.getByText('Activa', { exact: true }).waitFor(); const read = await api(student.context, 'GET', '/api/tutoring-sessions/' + session.id);
    assert.equal(read.turns.length, 1); assert.equal(read.messages.filter(m => m.role === 'Tutor').length, 1);
  });
  await check('Light/Dark reverse cycle, keyboard focus, reduced motion and private storage', async () => {
    const p = teacher.page; const toggle = p.getByRole('button', { name: 'Tema oscuro', exact: true });
    const original = await toggle.getAttribute('aria-pressed');
    await toggle.click();
    await p.waitForFunction(value => document.querySelector('app-theme-toggle button')?.getAttribute('aria-pressed') === value, original === 'true' ? 'false' : 'true');
    await toggle.click();
    await p.waitForFunction(value => document.querySelector('app-theme-toggle button')?.getAttribute('aria-pressed') === value, original);
    assert.equal(await toggle.getAttribute('aria-pressed'), original);
    await p.emulateMedia({ reducedMotion: 'reduce' }); await toggle.focus(); await p.keyboard.press('Tab');
    const observed = await p.evaluate(() => ({ keys: Object.keys(localStorage), sessionKeys: Object.keys(sessionStorage),
      outline: getComputedStyle(document.activeElement).outlineStyle, width: parseFloat(getComputedStyle(document.activeElement).outlineWidth),
      reduced: matchMedia('(prefers-reduced-motion: reduce)').matches }));
    assert.ok(observed.keys.every(k => k === 'aulatutor.theme.v1')); assert.equal(observed.sessionKeys.length, 0); assert.notEqual(observed.outline, 'none'); assert.ok(observed.width >= 2); assert.ok(observed.reduced);
  });
  let preloadedAssignment,preloadedSession;
  await check('IA-2 A: teacher generates one preloaded proposal, edits aids, simulates, publishes and assigns',async()=>{
    const example=await fs.readFile(path.join(root,'docs/architecture/examples/lesson-import-v2.json'),'utf8');
    const preview=await api(teacher.context,'POST','/api/lessons/import/preview',{json:example});assert.equal(preview.errors.length,0);assert.ok(preview.content.preloaded);
    const p=teacher.page;await p.goto(origin+'/teacher/lessons/new/ai');
    await p.getByLabel('Clase precargada: tarjetas y ayudas sin IA durante los ejercicios',{exact:true}).check();
    await p.getByLabel('Tema de la clase',{exact:true}).fill('Clase precargada IA-2 ficticia');
    await p.getByRole('button',{name:'Generar con IA integrada',exact:true}).click();await p.waitForURL(/mode=ai/);
    await mutation(p,'Aplicar propuesta',/\/apply-proposal$/);
    await p.getByRole('button',{name:'2. Contenido',exact:true}).click();
    await p.getByText('Contenido precargado y modalidad',{exact:true}).click();
    await p.getByLabel('Introducción (hasta 600 caracteres)',{exact:true}).fill('Introducción precargada IA-2 revisada por el docente.');
    await mutation(p,'Guardar borrador',/\/draft$/);
    await p.getByRole('button',{name:'3. Revisar y publicar',exact:true}).click();
    await mutation(p,'Simular como estudiante',/\/simulations$/);await p.waitForURL(/\/simulations\//);
    await mutation(p,'Comenzar enseñanza simulada',/\/messages$/);await mutation(p,'Pedir pista',/\/messages$/);await mutation(p,'Simplificar explicación',/\/messages$/);
    await mutation(p,'Detener simulación',/\/stop$/);await p.getByRole('link',{name:'← Volver al editor',exact:true}).click();
    await p.getByRole('button',{name:'3. Revisar y publicar',exact:true}).click();
    await mutation(p,'Pasar a revisión',/\/review$/);await mutation(p,'Confirmar aprobación de esta revisión',/\/approve$/);await mutation(p,'Confirmar publicación de versión 1',/\/publish$/);
    await p.getByRole('link',{name:'Asignar versión 1 a un grupo',exact:true}).click();await p.getByLabel('Grupo autorizado').selectOption(groupId);
    preloadedAssignment=await mutation(p,'Confirmar asignación',/\/api\/teacher\/assignments\/?$/);
  });
  await check('IA-2 B/E: cards, progressive local aids, lost response, pause/reload and completion despite staged AI quota',async()=>{
    const p=student.page;let blocked=0;await p.route('**/api/tutoring-sessions/*/messages',route=>{blocked++;return route.fulfill({status:429,contentType:'application/problem+json',body:JSON.stringify({title:'Cuota IA simulada; el resultado local se conserva.'})});});
    await p.goto(origin+'/classes/'+preloadedAssignment.id);await p.getByRole('button',{name:'Iniciar clase',exact:true}).click();await p.waitForURL(/\/tutoring\//);preloadedSession=p.url().split('/').at(-1);
    await mutation(p,'Comenzar clase precargada',/\/cards$/);
    await p.getByRole('button',{name:'Dame una pista',exact:true}).focus();await p.keyboard.press('Tab');
    const cardFocus=await p.evaluate(()=>({outline:getComputedStyle(document.activeElement).outlineStyle,width:parseFloat(getComputedStyle(document.activeElement).outlineWidth),keys:Object.keys(localStorage),session:Object.keys(sessionStorage)}));
    assert.notEqual(cardFocus.outline,'none');assert.ok(cardFocus.width>=2);assert.deepEqual(cardFocus.session,[]);assert.ok(cardFocus.keys.every(k=>k==='aulatutor.theme.v1'));
    for(const width of [320,390,1440])for(const dark of [false,true])await capture(p,`cards-${width}-${dark?'dark':'light'}`,width,dark,p.getByRole('heading',{name:'Introducción y práctica',exact:true}));
    await mutation(p,'Dame una pista',/\/cards$/);
    await p.route('**/api/tutoring-sessions/*/cards',async route=>{const response=await route.fetch();assert.ok(response.ok());await route.abort('failed');},{times:1});
    await p.getByRole('button',{name:'Dame una pista',exact:true}).click();await p.getByRole('button',{name:'Recuperar misma solicitud',exact:true}).waitFor();await mutation(p,'Recuperar misma solicitud',/\/cards$/);
    const afterHelp=await api(student.context,'GET','/api/tutoring-sessions/'+preloadedSession);assert.equal(afterHelp.learning.exercises[0].hints,2);
    await mutation(p,'Otra explicación',/\/cards$/);await mutation(p,'Pausar clase',/\/pause$/);await p.reload();await mutation(p,'Continuar clase',/\/resume$/);
    await p.getByLabel('Falso',{exact:true}).check();await mutation(p,'Registrar respuesta',/\/cards$/);await mutation(p,'Siguiente actividad',/\/cards$/);
    await p.getByLabel('Respuesta',{exact:true}).fill('Evidencia abierta ficticia IA-2.');await mutation(p,'Registrar respuesta',/\/cards$/);
    await mutation(p,'Finalizar clase precargada',/\/complete$/);assert.equal(blocked,0);
    const completed=await api(student.context,'GET','/api/tutoring-sessions/'+preloadedSession);assert.equal(completed.state,'Completed');assert.equal(completed.learning.reports[0].providerId,'Application');assert.equal(completed.learning.attempts.at(-1).outcome,'NeedsReview');
    await p.getByRole('button',{name:'Solicitar evaluación cualitativa final con IA',exact:true}).click();await p.getByText('Cuota IA simulada; el resultado local se conserva.',{exact:true}).waitFor();assert.equal(blocked,1);
    await capture(p,'cards-completed-no-quota-dark-390',390,true,p.getByText('Cuota IA simulada; el resultado local se conserva.',{exact:true}));
    await p.unroute('**/api/tutoring-sessions/*/messages');
  });
  await check('IA-2 C/D: grouped preliminary evaluation, reload without regeneration and independent human review',async()=>{
    const p=student.page;await mutation(p,'Solicitar evaluación cualitativa final con IA',/\/messages$/);
    await p.getByText('preloaded-evaluation-v1',{exact:false}).waitFor();const before=await api(student.context,'GET','/api/tutoring-sessions/'+preloadedSession);
    assert.equal(before.learning.reports.length,2);await p.reload();const after=await api(student.context,'GET','/api/tutoring-sessions/'+preloadedSession);assert.deepEqual(after.learning.reports,before.learning.reports);
    await capture(p,'cards-evaluation-light-1440',1440,false,p.getByText('preloaded-evaluation-v1',{exact:false}));
    const user=await api(student.context,'GET','/api/me');const data=await api(teacher.context,'GET',`/api/teacher/supervision/assignments/${preloadedAssignment.id}/students/${user.id}`);assert.ok(data.aiObservations.length>0);
    await teacher.page.goto(`${origin}/teacher/supervision/${preloadedAssignment.id}/students/${user.id}`);
    await teacher.page.getByLabel('Comentario para el estudiante').fill('Revisión humana IA-2 independiente de la evaluación preliminar.');await mutation(teacher.page,'Guardar decisión docente',/\/reviews$/);
    const reviewed=await api(student.context,'GET','/api/tutoring-sessions/'+preloadedSession);assert.deepEqual(reviewed.learning.attempts,after.learning.attempts);
  });
  const pilot=await academicPilot({api,login,mutation,capture,check,qa,origin,root,browser,admin,student});
  assert.equal(pageErrors.length, 0);
  await fs.writeFile(path.join(root, 'artifacts/iteration-9-e2e.json'), JSON.stringify({ status: 'passed', checks: results, javascriptErrors: pageErrors.length, preloadedSessionId:preloadedSession, pilot,elapsedMs:performance.now()-suiteStarted,requestMetrics:measuredRequests(),requestMetricScope:'api helper requests exclude CSRF setup; mutation timings include click/render until response. Raw negative-security calls and staged-error clicks outside helpers excluded. Per-route percentiles only for n>=20, nearest rank. Not provider latency.',scope: 'Prior 45 checks retained plus authored SQL pilot. Mock proves workflow, not pedagogy; 429/503 and lost responses explicitly staged.' }, null, 2));
  console.log(`E2E passed: ${results.length} checks; Mock only.`);
} catch (error) {
  const safe = String(error.message).split(qa.password).join('[redacted]');
  await fs.writeFile(path.join(root, 'artifacts/iteration-9-e2e.json'), JSON.stringify({ status: 'failed', step, message: safe, checks: results }, null, 2));
  if (lastPage && !lastPage.isClosed()) await lastPage.screenshot({ path: path.join(root, 'artifacts/iteration-9-e2e-failure.png'), animations: 'disabled' }).catch(() => {});
  console.error(`E2E failed at ${step}: ${safe}`); process.exitCode = 1;
} finally { if (browser) { for (const c of browser.contexts()) await c.clearCookies(); await browser.close(); } }

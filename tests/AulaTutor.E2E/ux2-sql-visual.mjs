// Supplemental visual QA on the retained, already verified fictional SQL fixture.
// This does not replace the complete E2E run and performs no academic mutations.
import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'../..');
process.env.TEMP=path.join(root,'.cache/e2e-temp');process.env.TMP=process.env.TEMP;
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.AULATUTOR_PLAYWRIGHT_MODULE||'playwright');
const qa=JSON.parse(await fs.readFile(path.join(root,'.cache/http-qa.json'),'utf8'));
const scenario=JSON.parse(await fs.readFile(path.join(root,'.cache/pilot-scenario.json'),'utf8'));
assert.match(qa.database,/^aulatutor_http_[a-f0-9]{32}$/);
assert.ok(scenario.teacherEmail.endsWith('@example.invalid'));
const assignment=scenario.classes.preloaded.id;assert.match(assignment,/^[a-f0-9-]{36}$/);
const origin='https://localhost:4200',screens=[],errors=[];
let browser;
try{
  browser=await chromium.launch({channel:'msedge',headless:true});
  const context=await browser.newContext({ignoreHTTPSErrors:true});const page=await context.newPage();
  page.on('pageerror',()=>errors.push('runtime error'));
  await page.route('**/*',route=>new URL(route.request().url()).hostname==='localhost'?route.continue():route.abort());
  const health=await (await context.request.get(origin+'/api/health')).json();assert.equal(health.aiProvider,'Mock');
  await page.goto(origin+'/login');await page.getByLabel('Correo electrónico').fill(scenario.teacherEmail);
  await page.getByLabel('Contraseña',{exact:true}).fill(qa.password);await page.getByRole('button',{name:'Entrar',exact:true}).click();await page.waitForURL(origin+'/');
  const read=async()=>{const r=await context.request.get(origin+'/api/teacher/supervision/assignments/'+assignment);assert.ok(r.ok());return r.json();};
  const before=await read();assert.equal(before.assignment.counts.eligibleStudents,3);assert.equal(before.assignment.counts.completed,2);assert.equal(before.assignment.counts.notOpened,1);
  assert.equal(before.objectives.reduce((sum,o)=>sum+o.eligibleRequiredSlots,0),12);
  assert.equal(before.objectives.reduce((sum,o)=>sum+o.deliveredRequired,0),8);
  const student=before.students.find(s=>s.attempts===8);assert.ok(student);
  await page.goto(origin+'/teacher/supervision/'+assignment);await page.getByRole('heading',{name:'Supervisión de clase',exact:true}).waitFor();
  async function capture(name,width,dark){
    const fit=await page.evaluate(()=>({width:innerWidth,scroll:document.documentElement.scrollWidth}));assert.ok(fit.scroll<=fit.width+1);
    await page.screenshot({path:path.join(root,'docs/iterations/evidence-ux-2',name+'.png'),animations:'disabled'});
    screens.push({name,width,dark,...fit});
  }
  for(const width of [390,1440])for(const dark of [false,true]){
    await page.setViewportSize({width,height:width<600?900:1000});
    const toggle=page.locator('app-theme-toggle button');if((await toggle.getAttribute('aria-pressed')==='true')!==dark)await toggle.click();
    await page.waitForFunction(value=>document.documentElement.dataset.theme===value,dark?'dark':'light');
    await page.evaluate(()=>scrollTo(0,0));await capture(`ux2-sql-supervision-${width}-${dark?'dark':'light'}`,width,dark);
    await page.getByRole('button',{name:'Ver detalle de '+student.name,exact:true}).click();
    const panel=page.getByRole('dialog',{name:student.name,exact:true});await panel.waitFor();
    await capture(`ux2-sql-drawer-${width}-${dark?'dark':'light'}`,width,dark);
    await page.keyboard.press('Escape');await panel.waitFor({state:'hidden'});
  }
  const after=await read();assert.deepEqual(after,before);assert.equal(errors.length,0);
  await fs.writeFile(path.join(root,'artifacts/ux2-sql-visual.json'),JSON.stringify({status:'passed',scope:'Eight supplemental screenshots; separate from full E2E',screens,javascriptErrors:errors.length,eligible:3,completed:2,notOpened:1,requiredOpportunities:12,deliveredRequired:8},null,2));
  console.log('SQL visual QA passed: 8 real screenshots, unchanged authorized cohort and no JavaScript errors.');
}catch(error){console.error(String(error.message).split(qa.password).join('[redacted]'));process.exitCode=1;}
finally{if(browser)await browser.close();}

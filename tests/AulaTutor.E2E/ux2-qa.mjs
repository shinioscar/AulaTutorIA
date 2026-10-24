import assert from 'node:assert/strict';

// Uses the existing isolated PostgreSQL/Mock fixture and real HTTPS endpoints.
export async function ux2Qa({teacher,student,admin,assignment,api,capture,check,origin}) {
  const p=teacher.page;
  await check('UX-2: real supervision charts, denominators and drawer keyboard lifecycle',async()=>{
    const data=await api(teacher.context,'GET',`/api/teacher/supervision/assignments/${assignment.id}`);
    const c=data.assignment.counts;
    assert.equal(c.notOpened+c.opened,c.eligibleStudents);
    await p.goto(origin+'/teacher/supervision');
    await p.getByRole('heading',{name:'Supervisión',exact:true}).waitFor();
    for(const width of [390,1440])for(const dark of [false,true])await capture(p,`ux2-supervision-home-${width}-${dark?'dark':'light'}`,width,dark);
    await p.goto(`${origin}/teacher/supervision/${assignment.id}`);
    await p.getByRole('heading',{name:'Supervisión de clase',exact:true}).waitFor();
    const chart=p.locator('app-supervision-charts');
    assert.equal(await chart.locator('tbody tr').count(),c.eligibleStudents?5:0);
    assert.equal(await chart.locator('progress').count(),data.objectives.length);
    const studentName=data.students[0].name;
    for(const width of [320,390,768,1024,1440])for(const dark of [false,true]){
      await capture(p,`ux2-supervision-${width}-${dark?'dark':'light'}`,width,dark,chart);
      const trigger=p.getByRole('button',{name:'Ver detalle de '+studentName,exact:true});
      await trigger.click();const panel=p.getByRole('dialog',{name:studentName,exact:true});await panel.waitFor();
      await panel.getByRole('button',{name:'Evidencias',exact:true}).click();
      await panel.getByRole('button',{name:'Feedback e historial',exact:true}).click();
      await panel.getByRole('button',{name:'Resumen',exact:true}).click();
      await capture(p,`ux2-drawer-${width}-${dark?'dark':'light'}`,width,dark);
      await panel.getByRole('button',{name:'Cerrar',exact:true}).focus();await p.keyboard.press('Shift+Tab');
      assert.ok(await p.evaluate(()=>!!document.activeElement?.closest('dialog')));
      for(let n=0;n<12;n++){await p.keyboard.press('Tab');assert.ok(await p.evaluate(()=>!!document.activeElement?.closest('dialog')));}
      await p.keyboard.press('Escape');await panel.waitFor({state:'hidden'});assert.ok(await trigger.evaluate(el=>el===document.activeElement));
    }
    await p.getByLabel('Buscar estudiante de esta clase').fill('sin-coincidencias-ux2');
    assert.equal(await p.locator('.student-row').count(),0);
    await p.getByLabel('Buscar estudiante de esta clase').fill('');
    await p.route('**/api/teacher/supervision/assignments/*/students/*',r=>r.fulfill({status:404,contentType:'application/problem+json',body:'{"title":"Recurso no disponible"}'}),{times:1});
    await p.getByRole('button',{name:'Ver detalle de '+studentName,exact:true}).click();
    const failed=p.getByRole('dialog');await failed.getByRole('alert').waitFor();
    assert.equal(await failed.locator('.drawer-tabs').count(),0);await p.keyboard.press('Escape');
  });
  await check('UX-2: library and AI configuration in five viewport sizes and both themes',async()=>{
    for(const [route,name,heading] of [['/teacher/lessons','library','Mis clases'],['/teacher/lessons/new/ai','create','Prepara tu clase con IA']]){
      await p.goto(origin+route);await p.getByRole('heading',{name:heading,exact:true}).waitFor();
      if(name==='create')await p.getByLabel('Tema de la clase',{exact:true}).fill('Clase ficticia UX-2');
      for(const width of [320,390,768,1024,1440])for(const dark of [false,true])await capture(p,`ux2-${name}-${width}-${dark?'dark':'light'}`,width,dark);
    }
  });
  await check('UX-2: mobile menu, role navigation, Escape and reduced motion',async()=>{
    for(const actor of [teacher,student,admin]){
      const page=actor.page;await page.goto(origin+'/');await page.setViewportSize({width:390,height:900});
      const menu=page.getByRole('button',{name:'Menú',exact:true});await menu.click();
      const nav=page.getByRole('navigation',{name:'Navegación principal'});await nav.waitFor();
      const user=await api(actor.context,'GET','/api/me');
      assert.equal(await nav.getByRole('link',{name:'Supervisión docente',exact:true}).count(),user.roles.includes('Teacher')?1:0);
      assert.equal(await nav.getByRole('link',{name:'Administración',exact:true}).count(),user.roles.includes('Admin')?1:0);
      await nav.getByRole('link',{name:'Inicio',exact:true}).focus();await page.keyboard.press('Escape');
      await page.waitForFunction(()=>document.querySelector('.menu-toggle')?.getAttribute('aria-expanded')==='false');
      assert.equal(await menu.getAttribute('aria-expanded'),'false');assert.ok(await menu.evaluate(el=>el===document.activeElement));
      await page.emulateMedia({reducedMotion:'reduce'});
      assert.equal(await menu.evaluate(el=>getComputedStyle(el).transitionDuration),'0s');
      assert.deepEqual(await page.evaluate(()=>Object.keys(sessionStorage)),[]);
    }
  });
  await check('UX-2: declining navigation preserves an unsaved lesson draft',async()=>{
    const lessons=await api(teacher.context,'GET','/api/lessons/?page=1');
    const draft=lessons.find(l=>l.state==='Draft');assert.ok(draft);
    await p.goto(origin+'/teacher/lessons/'+draft.id);
    const title=p.getByLabel('Título',{exact:true});await title.waitFor();
    const saved=await title.inputValue();const local=saved+' · cambio local';await title.fill(local);
    p.removeAllListeners('dialog');p.once('dialog',d=>d.dismiss());
    await p.getByRole('link',{name:'← Mis clases',exact:true}).click();
    assert.equal(await title.inputValue(),local);
    p.once('dialog',d=>d.accept());await p.getByRole('link',{name:'← Mis clases',exact:true}).click();
    await p.waitForURL(origin+'/teacher/lessons');p.on('dialog',d=>d.accept());
    const unchanged=await api(teacher.context,'GET','/api/lessons/'+draft.id);assert.equal(unchanged.content.title,saved);
  });
  await check('UX-2: visual coherence across administrative and shared modules',async()=>{
    for(const [actor,route,name] of [[admin,'/admin/users','admin-users'],[admin,'/groups','groups'],[admin,'/admin/ai','admin-ai'],[teacher,'/teacher/assignments','assignments'],[teacher,'/teacher/reports','shared-reports'],[student,'/tutoring','tutoring-list']]){
      const page=actor.page;await page.goto(origin+route);await page.locator('h1').first().waitFor();await page.waitForLoadState('networkidle');
      for(const width of [390,1440])for(const dark of [false,true])await capture(page,`ux2-${name}-${width}-${dark?'dark':'light'}`,width,dark);
    }
  });
}

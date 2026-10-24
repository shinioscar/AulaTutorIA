import assert from 'node:assert/strict';

// Real HTTPS fixture, existing role guards and Mock; never changes provider settings.
export async function uxPolishQa({teacher,student,admin,assignment,api,capture,check,origin}) {
  await check('UX polish: focused home by role without provider requests',async()=>{
    for(const [actor,name] of [[teacher,'teacher'],[student,'student'],[admin,'admin']]){
      const p=actor.page,requests=[];
      const record=r=>{if(new URL(r.url()).pathname==='/api/health'||r.url().includes('/api/admin/ai'))requests.push(r.url());};
      p.on('request',record);
      try{
        await p.goto(origin+'/');await p.getByRole('navigation',{name:'Accesos de inicio',exact:true}).waitFor();await p.waitForLoadState('networkidle');
        assert.equal(requests.length,0);
        assert.equal(await p.getByRole('heading',{name:'Estado del entorno local',exact:true}).count(),0);
        assert.equal(await p.getByRole('heading').count(),1);
        for(const width of [320,390,1440])for(const dark of [false,true])await capture(p,`polish-home-${name}-${width}-${dark?'dark':'light'}`,width,dark);
      }finally{p.off('request',record);}
    }
  });
  await check('UX polish/BYOK: teacher provider page reads own configuration without automatic inference',async()=>{
    const p=teacher.page,requests=[];
    const record=r=>{const route=new URL(r.url()).pathname;if(route.startsWith('/api/'))requests.push({route,method:r.method()});};
    p.on('request',record);
    try{
      await p.setViewportSize({width:1440,height:1000});await p.goto(origin+'/');
      await p.getByRole('navigation',{name:'Navegación principal',exact:true}).getByRole('link',{name:'Proveedor de IA',exact:true}).click();
      await p.waitForURL(origin+'/ai-provider');await p.getByRole('heading',{name:'Proveedor institucional',exact:true}).waitFor();
      assert.equal(await p.getByRole('button',{name:'Ejecutar prueba breve',exact:true}).count(),0);
      assert.ok(requests.some(r=>r.route==='/api/ai-connections/'));
      assert.equal(requests.filter(r=>r.route.startsWith('/api/admin/')||r.method==='POST').length,0);
      for(const width of [320,390,768,1024,1440])for(const dark of [false,true])await capture(p,`polish-provider-teacher-${width}-${dark?'dark':'light'}`,width,dark);
      await p.route('**/api/ai-connections/',r=>r.fulfill({status:503,contentType:'application/json',body:'{"title":"Conexión interrumpida. Intenta actualizar."}'}),{times:1});
      await p.getByRole('button',{name:'Actualizar estado',exact:true}).click();
      await p.getByText('Conexión interrumpida. Intenta actualizar.',{exact:true}).waitFor();
      assert.equal(await p.getByRole('heading',{name:'Proveedor institucional',exact:true}).count(),0);
      await p.getByRole('button',{name:'Actualizar estado',exact:true}).click();await p.getByRole('heading',{name:'Proveedor institucional',exact:true}).waitFor();
      assert.equal((await teacher.context.request.get(origin+'/api/admin/ai/')).status(),403);
    }finally{p.off('request',record);}
  });
  await check('UX polish/BYOK: admin diagnostic stays explicit and Student has own provider page',async()=>{
    const p=admin.page;let probes=0;
    const record=r=>{if(r.url().endsWith('/api/admin/ai/test')&&r.method()==='POST')probes++;};p.on('request',record);
    try{
      await p.goto(origin+'/admin/ai');await p.getByRole('heading',{name:'Configuración y diagnóstico',exact:true}).waitFor();await p.waitForLoadState('networkidle');
      assert.equal(probes,0,'Opening configuration must not run a diagnostic');assert.equal(await p.locator('details[open]').count(),0);
      for(const width of [320,390,768,1024,1440])for(const dark of [false,true])await capture(p,`polish-provider-admin-${width}-${dark?'dark':'light'}`,width,dark);
      const health=await api(admin.context,'GET','/api/health');assert.equal(health.aiProvider,'Mock');
      await p.getByRole('button',{name:'Ejecutar prueba breve',exact:true}).click();await p.getByRole('heading',{name:'Prueba completada',exact:true}).waitFor();assert.equal(probes,1,'The explicit Admin probe makes exactly one request');
      await p.getByRole('button',{name:'Actualizar estado',exact:true}).click();
      await p.getByRole('heading',{name:'Prueba completada',exact:true}).waitFor({state:'hidden'});
      await p.getByRole('button',{name:'Ejecutar prueba breve',exact:true}).waitFor({state:'visible'});
      assert.equal(await p.getByRole('heading',{name:'Prueba completada',exact:true}).count(),0,'Refreshing clears the earlier diagnostic');assert.equal(probes,1,'Refreshing must not repeat the diagnostic');
    }finally{p.off('request',record);}
    await student.page.goto(origin+'/');const nav=student.page.getByRole('navigation',{name:'Navegación principal',exact:true});
    await nav.getByRole('link',{name:'Proveedor de IA',exact:true}).waitFor({state:'visible'});
    assert.equal(await nav.getByRole('link',{name:'Proveedor de IA',exact:true}).count(),1,'Student gets exactly one own-provider navigation entry');
    await student.page.goto(origin+'/ai-provider');await student.page.getByRole('heading',{name:'Proveedor institucional',exact:true}).waitFor();
    await student.page.goto(origin+'/admin/ai');await student.page.waitForURL(origin+'/');
    assert.equal((await student.context.request.get(origin+'/api/admin/ai/')).status(),403);
  });
  await check('UX polish: concise supervision retains actual report values and expandable details',async()=>{
    const p=teacher.page,data=await api(teacher.context,'GET',`/api/teacher/supervision/assignments/${assignment.id}`);
    await p.goto(`${origin}/teacher/supervision/${assignment.id}`);await p.locator('app-supervision-charts').waitFor();
    assert.equal(await p.getByText('El avance no es una calificación.',{exact:false}).count(),0);
    for(const width of [390,1440])for(const dark of [false,true])await capture(p,`polish-supervision-${width}-${dark?'dark':'light'}`,width,dark,p.locator('app-supervision-charts'));
    await p.getByRole('button',{name:'Reporte académico',exact:true}).click();
    const row=p.locator('.objective-report').first();await row.locator('summary').click();
    assert.equal(await row.locator('dd').nth(2).innerText(),`${data.objectives[0].deliveredRequired} / ${data.objectives[0].eligibleRequiredSlots}`);
    await capture(p,'polish-supervision-report-1440-light',1440,false,row);
  });
}

import assert from 'node:assert/strict';

export async function byokQa({teacher,student,admin,assignment,api,capture,check,origin,login,qa}) {
  const widths=[320,390,768,1440];
  const matrix=async(p,name,focus=null)=>{for(const width of widths)for(const dark of [false,true])await capture(p,`byok-${name}-${width}-${dark?'dark':'light'}`,width,dark,focus);};
  const fakeConsent=async route=>{
    const auth=new URL(route.request().url());assert.equal(auth.origin,'https://openrouter.ai');assert.equal(auth.searchParams.get('code_challenge_method'),'S256');
    const callback=new URL(auth.searchParams.get('callback_url'));assert.equal(callback.origin,origin);assert.equal(callback.pathname,'/api/ai-connections/openrouter/callback');
    callback.searchParams.set('state',auth.searchParams.get('state'));callback.searchParams.set('code','qa_fictitious_one_time_code');
    await route.fulfill({contentType:'text/html; charset=utf-8',headers:{'Cross-Origin-Opener-Policy':'same-origin'},body:`<!doctype html><html lang="es"><title>Autorización simulada</title><body><h1>OpenRouter · simulación aislada</h1><a href="${callback.href.replaceAll('&','&amp;')}">Autorizar conexión simulada</a></body></html>`});
  };
  const intercept=async actor=>{await actor.context.route('https://openrouter.ai/auth**',fakeConsent);await actor.page.route('https://openrouter.ai/auth**',fakeConsent);};
  let teacherConnection,studentConnection,personalSession;
  await check('BYOK: Teacher and Student own provider pages, no automatic inference, responsive themes',async()=>{
    for(const [actor,name] of [[teacher,'teacher'],[student,'student']]){
      await actor.page.goto(origin+'/ai-provider');await actor.page.getByRole('heading',{name:'OpenRouter',exact:true}).waitFor();
      const state=await api(actor.context,'GET','/api/ai-connections/');assert.equal(state.selection,'Institutional');assert.equal(state.custodyAvailable,true);
      await matrix(actor.page,`${name}-initial`);assert.equal((await actor.context.request.get(origin+'/api/admin/ai/')).status(),403);
      await intercept(actor);
    }
  });
  await check('BYOK: official popup PKCE callback, free model and teacher proposal use own simulated connection',async()=>{
    const p=teacher.page;await p.setViewportSize({width:1440,height:1000});
    const [popup]=await Promise.all([p.waitForEvent('popup'),p.getByRole('button',{name:'Conectar OpenRouter',exact:true}).click()]);
    await popup.getByRole('link',{name:'Autorizar conexión simulada',exact:true}).click();
    await p.getByText('OpenRouter conectado. Elige un modelo para activarlo.',{exact:true}).waitFor();
    teacherConnection=(await api(teacher.context,'GET','/api/ai-connections/')).connections.find(c=>c.provider==='OpenRouter');assert.ok(teacherConnection);
    assert.equal(teacherConnection.status,'Connected');assert.equal(teacherConnection.method,'OAuth');assert.equal('apiKey' in teacherConnection,false);
    await p.getByRole('button',{name:'Elegir modelo',exact:true}).click();await p.getByLabel('Modelo',{exact:true}).waitFor();
    assert.equal(await p.getByRole('option',{name:/Modelo de pago/}).count(),0);
    assert.equal(await p.getByText('Mostrar modelos de pago o precio sin confirmar',{exact:true}).count(),0);
    assert.equal(await p.locator('option[value="qa/structured:free"]').count(),1);
    assert.match(await p.locator('option[value="qa/structured:free"]').textContent(),/Gratis/);
    assert.equal(await p.getByRole('option',{name:/Modelo incompatible/}).isDisabled(),true);
    await matrix(p,'teacher-models',p.locator('.connection-editor'));
    await p.getByLabel(/Acepto enviar mis consultas/).check();await p.getByRole('button',{name:'Activar modelo',exact:true}).click();
    await p.getByText('OpenRouter activado para nuevas consultas.',{exact:true}).waitFor();
    await p.goto(origin+'/teacher/lessons/new/ai');await p.getByLabel('Tema de la clase',{exact:true}).fill('Clase ficticia BYOK');
    await p.getByRole('button',{name:'Generar con IA integrada',exact:true}).click();await p.waitForURL(/mode=ai/);
    await p.getByRole('button',{name:'Aplicar propuesta',exact:true}).waitFor();
    const lesson=await api(teacher.context,'GET',new URL(p.url()).pathname.replace('/teacher/lessons/','/api/lessons/'));
    assert.equal(lesson.operations.at(-1).state,'Succeeded');assert.equal(lesson.operations.at(-1).providerId,'OpenRouter');assert.equal(lesson.operations.at(-1).modelId,'qa/structured:free');
    await capture(p,'byok-teacher-proposal-1440-light',1440,false,p.getByRole('button',{name:'Aplicar propuesta',exact:true}));
    await p.goto(origin+'/ai-provider');await p.getByRole('button',{name:'Usar institucional',exact:true}).click();await p.getByText('Proveedor institucional seleccionado para nuevas consultas.',{exact:true}).waitFor();
  });
  await check('BYOK: Student mobile full redirect and session snapshot survives changing future selection',async()=>{
    const p=student.page;await p.setViewportSize({width:390,height:900});await p.goto(origin+'/ai-provider');
    await p.getByRole('button',{name:'Conectar OpenRouter',exact:true}).click();await p.getByRole('link',{name:'Autorizar conexión simulada',exact:true}).click();
    await p.getByRole('heading',{name:'OpenRouter conectado',exact:true}).waitFor();assert.equal(new URL(p.url()).search,'');
    await p.getByRole('link',{name:'Ir a Proveedor de IA',exact:true}).click();await p.getByRole('button',{name:'Elegir modelo',exact:true}).click();await p.getByLabel('Modelo',{exact:true}).waitFor();
    await matrix(p,'student-models',p.locator('.connection-editor'));
    await p.getByLabel(/Acepto enviar mis consultas/).check();await p.getByRole('button',{name:'Activar modelo',exact:true}).click();await p.getByText('OpenRouter activado para nuevas consultas.',{exact:true}).waitFor();
    studentConnection=(await api(student.context,'GET','/api/ai-connections/')).connections.find(c=>c.provider==='OpenRouter');
    const session=await api(student.context,'POST','/api/tutoring-sessions/',{requestId:crypto.randomUUID(),topic:'Condiciones SQL ficticias BYOK',goal:'Practicar con una conexión propia',level:'Beginner',directReview:false});personalSession=session.id;
    const started=await api(student.context,'POST',`/api/tutoring-sessions/${session.id}/messages`,{requestId:crypto.randomUUID(),revision:session.revision,action:'Start'});assert.equal(started.turns.at(-1).state,'Succeeded');assert.equal(started.messages.at(-1).providerId,'OpenRouter');assert.equal(started.messages.at(-1).usage,null);
    await p.goto(origin+'/ai-provider');await p.getByRole('button',{name:'Usar institucional',exact:true}).click();await p.getByText('Proveedor institucional seleccionado para nuevas consultas.',{exact:true}).waitFor();
    const next=await api(student.context,'POST',`/api/tutoring-sessions/${session.id}/messages`,{requestId:crypto.randomUUID(),revision:started.revision,action:'Message',text:'Comparo dos condiciones ficticias.'});assert.equal(next.messages.at(-1).providerId,'OpenRouter');assert.equal(next.turns.at(-1).state,'Succeeded');
    const future=await api(student.context,'POST','/api/tutoring-sessions/',{requestId:crypto.randomUUID(),topic:'Otra tutoría ficticia',goal:'Conservar selección institucional',level:'Beginner',directReview:false});assert.equal(future.providerId,'Mock');
  });
  await check('BYOK: Gemini, Claude and Groq manual forms hide keys, clear input and disconnect own connection',async()=>{
    const actor=await login(qa.tutoringOwnerEmail),p=actor.page;
    try{
      for(const [id,name] of [['Gemini','Gemini'],['Anthropic','Claude'],['Groq','Groq']]){
        await p.goto(origin+'/ai-provider');await p.getByRole('button',{name:'Conectar '+name,exact:true}).click();
        await matrix(p,`manual-${id.toLowerCase()}`,p.locator('.connection-editor'));
        assert.equal(await p.getByLabel('Clave de API',{exact:true}).getAttribute('type'),'password');
        await p.getByLabel('Clave de API',{exact:true}).fill('qa_manual_fictitious_credential_only');await p.getByRole('button',{name:'Guardar clave',exact:true}).click();await p.getByText('Clave guardada. Elige un modelo para activarla.',{exact:true}).waitFor();
        assert.equal(await p.getByLabel('Clave de API',{exact:true}).count(),0);const view=await api(actor.context,'GET','/api/ai-connections/');assert.equal(view.connections.find(c=>c.provider===id).status,'Connected');assert.equal(JSON.stringify(view).includes('qa_manual_fictitious'),false);
        await p.getByRole('button',{name:'Elegir modelo de '+name,exact:true}).click();
        await p.getByText('No hay modelos gratuitos compatibles con precio confirmado en este catálogo. Puedes actualizarlo para comprobar cambios.',{exact:true}).waitFor();
        assert.equal(await p.getByLabel('Modelo',{exact:true}).count(),0);
        assert.equal(await p.getByText('No se pudo cargar el catálogo. Intenta actualizarlo.',{exact:true}).count(),0);
        const storage=await p.evaluate(()=>({local:Object.keys(localStorage),session:Object.keys(sessionStorage)}));assert.ok(storage.local.every(k=>k==='aulatutor.theme.v1'));assert.deepEqual(storage.session,[]);assert.equal(new URL(p.url()).search,'');
        await p.getByRole('button',{name:'Desconectar '+name,exact:true}).click();await p.getByText(name+' desconectado de AulaTutor.',{exact:true}).waitFor();
      }
    }finally{await actor.context.close();}
  });
  await check('BYOK: preloaded local cards retain zero calls and grouped evaluation uses the Student own pinned key',async()=>{
    const actor=await login(qa.tutoringOwnerEmail);
    try{
      const user=await api(actor.context,'GET','/api/me');
      await api(admin.context,'PUT',`/api/groups/${assignment.groupId}/members`,{userId:user.id,kind:'Student'});
      await api(actor.context,'POST','/api/ai-connections/manual',{provider:'Groq',apiKey:'qa_manual_fictitious_credential_only'});
      const connection=(await api(actor.context,'GET','/api/ai-connections/')).connections.find(c=>c.provider==='Groq');
      await api(actor.context,'GET','/api/ai-connections/models/Groq');
      await api(actor.context,'PUT','/api/ai-connections/selection',{connectionId:connection.id,model:'openai/gpt-oss-20b',privacyAccepted:true,paidAccepted:true});
      let s=await api(actor.context,'POST',`/api/students/me/classes/${assignment.id}/start`,{requestId:crypto.randomUUID()});assert.equal(s.providerId,'Groq');
      await api(actor.context,'PUT','/api/ai-connections/selection',{connectionId:null,model:null,privacyAccepted:false,paidAccepted:false});
      const reserved=async()=> (await api(admin.context,'GET','/api/admin/operations/usage')).providers.reduce((sum,p)=>sum+p.reservations,0);
      const before=await reserved();
      const local=async(action,text=null)=>{s=await api(actor.context,'POST',`/api/tutoring-sessions/${s.id}/cards`,{requestId:crypto.randomUUID(),revision:s.revision,action,text,exerciseId:action==='SubmitAttempt'?s.assignedClass.currentExerciseId:null});};
      await local('Start');await local('Hint');await local('SubmitAttempt','Falso');await local('NextActivity');await local('SubmitAttempt','Evidencia abierta ficticia BYOK.');
      s=await api(actor.context,'POST',`/api/tutoring-sessions/${s.id}/complete`,{revision:s.revision});assert.equal(s.state,'Completed');assert.equal(s.learning.attempts.at(-1).outcome,'NeedsReview');assert.equal(await reserved(),before);
      const evidence=structuredClone(s.learning.attempts),request={requestId:crypto.randomUUID(),revision:s.revision,action:'FinalReport'};
      s=await api(actor.context,'POST',`/api/tutoring-sessions/${s.id}/messages`,request);assert.equal(s.turns.at(-1).state,'Succeeded');assert.equal(s.learning.reports.find(r=>r.promptVersion==='preloaded-evaluation-v1')?.providerId,'Groq');assert.deepEqual(s.learning.attempts,evidence);assert.equal(await reserved(),before+1);
      await api(actor.context,'DELETE','/api/ai-connections/'+connection.id);
      const replay=await api(actor.context,'POST',`/api/tutoring-sessions/${s.id}/messages`,request);assert.deepEqual(replay.learning.reports,s.learning.reports);assert.equal(await reserved(),before+1);
      const usage=await api(admin.context,'GET','/api/admin/operations/usage');assert.ok(usage.providers.some(p=>p.scope==='PersonalQa'&&p.provider==='Groq'&&p.succeeded===1));
      await actor.page.goto(origin+'/tutoring/'+s.id);
      const report=actor.page.locator('app-report-card').filter({hasText:'preloaded-evaluation-v1'});await report.waitFor();
      await report.getByText('Origen del reporte',{exact:true}).click();await capture(actor.page,'byok-preloaded-evaluation-1440-light',1440,false,report);
    }finally{await actor.context.close();}
  });
  await check('BYOK: popup blocked fallback, canceled transaction and rejected callback are actionable',async()=>{
    const p=teacher.page;await p.setViewportSize({width:1440,height:1000});
    // Reconnection entry is exercised through a disconnected local connection.
    await p.goto(origin+'/ai-provider');await p.getByRole('button',{name:'Desconectar OpenRouter',exact:true}).click();await p.getByText('OpenRouter desconectado de AulaTutor.',{exact:true}).waitFor();
    await p.evaluate(()=>{window.__qaOriginalOpen=window.open;window.open=()=>null;});
    await p.getByRole('button',{name:'Conectar OpenRouter',exact:true}).click();await p.getByRole('button',{name:'Continuar en esta pestaña',exact:true}).waitFor();
    await capture(p,'byok-popup-blocked-1440-dark',1440,true,p.getByRole('button',{name:'Continuar en esta pestaña',exact:true}));
    await p.getByRole('button',{name:'Cancelar autorización',exact:true}).click();await p.getByText('Autorización cancelada. Puedes volver a conectar.',{exact:true}).waitFor();
    await p.evaluate(()=>{window.open=window.__qaOriginalOpen;delete window.__qaOriginalOpen;});
    // Restore Teacher connection through a third explicit authorization, leaving it encrypted for restart QA.
    const [popup]=await Promise.all([p.waitForEvent('popup'),p.getByRole('button',{name:'Conectar OpenRouter',exact:true}).click()]);await popup.getByRole('link',{name:'Autorizar conexión simulada',exact:true}).click();await p.getByText('OpenRouter conectado. Elige un modelo para activarlo.',{exact:true}).waitFor();
    teacherConnection=(await api(teacher.context,'GET','/api/ai-connections/')).connections.find(c=>c.provider==='OpenRouter');
    await student.page.goto(origin+'/api/ai-connections/openrouter/callback?state=invalid&code=qa_fictitious');await student.page.getByRole('heading',{name:'Autorización no completada',exact:true}).waitFor();assert.equal(new URL(student.page.url()).search,'?result=rejected');
    await matrix(student.page,'callback-rejected',student.page.getByRole('heading',{name:'Autorización no completada',exact:true}));
  });
  await check('BYOK: actual simulated quota keeps the attempt and exposes an actionable error without fallback',async()=>{
    const p=student.page;await p.goto(origin+'/ai-provider');await p.getByRole('button',{name:'Elegir modelo',exact:true}).click();await p.getByLabel('Modelo',{exact:true}).selectOption('qa/limited:free');
    await p.getByLabel(/Acepto enviar mis consultas/).check();await p.getByRole('button',{name:'Activar modelo',exact:true}).click();await p.getByText('OpenRouter activado para nuevas consultas.',{exact:true}).waitFor();
    const session=await api(student.context,'POST','/api/tutoring-sessions/',{requestId:crypto.randomUUID(),topic:'Límite ficticio BYOK',goal:'Conservar el intento durante una espera',level:'Beginner',directReview:false});
    await p.goto(origin+'/tutoring/'+session.id);await p.getByRole('button',{name:'Comenzar conversación',exact:true}).click();await p.getByText('Se alcanzó un límite de solicitudes o presupuesto. Espera antes de reintentar.',{exact:true}).waitFor();
    const failed=await api(student.context,'GET','/api/tutoring-sessions/'+session.id);assert.equal(failed.turns.at(-1).errorCode,'RateLimited');assert.equal(failed.messages.filter(m=>m.role==='Tutor').length,0);assert.equal(failed.providerId,'OpenRouter');
    await matrix(p,'quota-error',p.getByText('Se alcanzó un límite de solicitudes o presupuesto. Espera antes de reintentar.',{exact:true}));
    await p.goto(origin+'/ai-provider');await p.getByRole('button',{name:'Usar institucional',exact:true}).click();await p.getByText('Proveedor institucional seleccionado para nuevas consultas.',{exact:true}).waitFor();
  });
  return {status:'passed',studentSessionId:personalSession,studentConnectionId:studentConnection.id,teacherConnectionId:teacherConnection.id,scope:'Only isolated simulated OAuth and HTTP; no external account, keys or inference.'};
}

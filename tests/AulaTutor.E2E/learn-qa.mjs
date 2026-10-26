import assert from 'node:assert/strict';

export async function learnQa({student,teacher,admin,api,mutation,capture,check,origin,byok}) {
  // Earlier BYOK journeys also send deliberately untracked direct requests. Let their
  // real production limiter/cooldown expire; never weaken policy to make QA pass.
  for(let window=0;window<2;window++)await new Promise(resolve=>setTimeout(resolve,31000));
  const p=student.page, sessions=[];let tutoring,diagnostic,goalSession;
  const matrix=async(name,focus=null)=>{for(const width of [320,390,768,1440])for(const dark of [false,true])await capture(p,`learn-${name}-${width}-${dark?'dark':'light'}`,width,dark,focus);};
  const create=async(experience)=>{
    await p.goto(origin+'/tutoring');await p.getByRole('button',{name:experience==='Diagnostic'?'Diagnósticos':'Tutorías',exact:true}).click();
    await p.getByRole('button',{name:experience==='Diagnostic'?/Menos solicitudes.*Diagnóstico preparado/:/Menos solicitudes.*Tutoría preparada/}).click();
    await p.getByLabel('Tema',{exact:true}).fill('Evidencia ficticia LEARN-1');await p.getByLabel('Objetivo de aprendizaje (opcional)',{exact:true}).fill('Contrastar casos y recoger evidencia');
    const s=await mutation(p,experience==='Diagnostic'?'Crear diagnóstico preparado':'Crear tutoría preparada',/\/api\/tutoring-sessions\/?$/);
    await p.waitForURL(origin+'/tutoring/'+s.id);sessions.push(s.id);return s;
  };
  const play=async(isDiagnostic)=>{
    let s=await api(student.context,'GET','/api/tutoring-sessions/'+new URL(p.url()).pathname.split('/').at(-1));
    while(s.prepared.currentExerciseId){
      const e=s.learning.exercises.find(e=>e.id===s.prepared.currentExerciseId);
      if(e.options.length)await p.getByLabel('Falso',{exact:true}).check();else await p.getByLabel('Respuesta',{exact:true}).fill('Otra evidencia ficticia; este texto no se ejecuta.');
      s=await mutation(p,'Registrar respuesta',/\/cards$/);
      if(isDiagnostic)assert.ok(s.learning.attempts.every(a=>a.outcome==='Recorded'&&a.validationType==='Deferred'));
      s=await mutation(p,'Siguiente actividad',/\/cards$/);
    }
    return mutation(p,isDiagnostic?'Finalizar diagnóstico':'Finalizar tutoría preparada',/\/complete$/);
  };
  await check('LEARN-1: modality cards, compatible lengths, responsive themes and keyboard',async()=>{
    await api(student.context,'PUT','/api/ai-connections/selection',{connectionId:null,model:null,privacyAccepted:false,paidAccepted:false});
    await p.goto(origin+'/tutoring');await p.getByRole('button',{name:/Menos solicitudes.*Tutoría preparada/}).click();
    await matrix('selection',p.getByRole('heading',{name:'¿Qué quieres aprender?',exact:true}));
    await p.getByRole('button',{name:/Menos solicitudes.*Tutoría preparada/}).focus();await p.keyboard.press('Tab');
    assert.notEqual(await p.evaluate(()=>getComputedStyle(document.activeElement).outlineStyle),'none');
    await p.getByRole('button',{name:'Diagnósticos',exact:true}).click();await matrix('diagnostic-selection');
    await p.getByRole('button',{name:/^Diagnóstico conversacional/}).click();
    await p.getByRole('button',{name:'Crear diagnóstico conversacional',exact:true}).waitFor();
    await p.getByRole('button',{name:'Tutorías',exact:true}).click();await p.getByRole('button',{name:/^Tutoría conversacional/}).click();
    await p.getByRole('button',{name:'Crear tutoría privada',exact:true}).waitFor();
  });
  await check('LEARN-1 C: conversational diagnostic selection retains existing question and answer inference',async()=>{
    await p.goto(origin+'/tutoring');await p.getByRole('button',{name:'Diagnósticos',exact:true}).click();await p.getByRole('button',{name:/^Diagnóstico conversacional/}).click();
    await p.getByLabel('Tema',{exact:true}).fill('Diagnóstico conversacional ficticio');
    const s=await mutation(p,'Crear diagnóstico conversacional',/\/api\/tutoring-sessions\/?$/);await p.waitForURL(origin+'/tutoring/'+s.id);assert.equal(s.prepared,null);
    let active=await mutation(p,'Comenzar diagnóstico estructurado',/\/messages$/);assert.equal(active.learning.exercises[0].isDiagnostic,true);
    await p.getByLabel('Respuesta de este ejercicio',{exact:true}).fill('Una explicación ficticia para conservar el flujo conversacional.');
    active=await mutation(p,'Registrar respuesta',/\/messages$/);assert.equal(active.turns.length,2);assert.equal(active.learning.attempts[0].outcome,'NeedsReview');
    await mutation(p,'Finalizar tutoría',/\/complete$/);
  });
  await check('LEARN-1 A/F: prepared tutoring, lost local response, help, pause/reload, zero intermediate AI and local close',async()=>{
    tutoring=await create('Tutoring');await mutation(p,'Preparar tutoría',/\/messages$/);await mutation(p,'Comenzar tutoría preparada',/\/cards$/);
    await matrix('tutoring-activity',p.locator('app-prepared-cards .exercise-card'));
    let calls=0;await p.route('**/api/tutoring-sessions/*/messages',route=>{calls++;return route.fulfill({status:429,contentType:'application/problem+json',body:JSON.stringify({title:'Cuota simulada agotada. Evidencias conservadas.'})});});
    await p.route('**/api/tutoring-sessions/*/cards',async route=>{const r=await route.fetch();assert.ok(r.ok());await route.abort('failed');},{times:1});
    await p.getByRole('button',{name:'Dame una pista',exact:true}).click();await p.getByRole('button',{name:'Recuperar misma solicitud',exact:true}).waitFor();
    await mutation(p,'Recuperar misma solicitud',/\/cards$/);let s=await api(student.context,'GET','/api/tutoring-sessions/'+tutoring.id);assert.equal(s.learning.exercises[0].hints,1);
    await mutation(p,'Otra explicación',/\/cards$/);await mutation(p,'Pausar recorrido',/\/pause$/);await p.reload();await mutation(p,'Reanudar recorrido',/\/resume$/);
    s=await play(false);assert.equal(s.state,'Completed');assert.equal(calls,0);assert.equal(s.learning.reports.length,1);
    await p.getByRole('button',{name:'Solicitar evaluación final con IA',exact:true}).click();await p.getByText('Cuota simulada agotada. Evidencias conservadas.',{exact:true}).waitFor();assert.equal(calls,1);
    await matrix('local-report-no-quota');await p.unroute('**/api/tutoring-sessions/*/messages');
    await p.route('**/api/tutoring-sessions/*/messages',route=>route.fulfill({status:429,body:''}),{times:1});
    await p.getByRole('button',{name:'Solicitar evaluación final con IA',exact:true}).click();
    await p.getByText('Se alcanzó el límite de solicitudes. Espera antes de volver a solicitar IA. Tus respuestas y resultados se conservan.',{exact:true}).waitFor();
    assert.equal((await api(student.context,'GET','/api/tutoring-sessions/'+tutoring.id)).learning.reports.length,1);
    tutoring=await mutation(p,'Solicitar evaluación final con IA',/\/messages$/);assert.equal(tutoring.learning.reports.length,2);
    await p.reload();assert.deepEqual((await api(student.context,'GET','/api/tutoring-sessions/'+tutoring.id)).learning.reports,tutoring.learning.reports);
  });
  await check('LEARN-1 B: diagnostic hides results, final analysis and plan together, explicit accepted prepared goal',async()=>{
    diagnostic=await create('Diagnostic');await mutation(p,'Preparar diagnóstico',/\/messages$/);await mutation(p,'Comenzar diagnóstico',/\/cards$/);
    assert.equal(await p.getByRole('button',{name:'Dame una pista',exact:true}).count(),0);await matrix('diagnostic-activity');
    diagnostic=await play(true);diagnostic=await mutation(p,'Analizar resultados y preparar mi plan',/\/messages$/);
    assert.ok(diagnostic.learning.plan);assert.ok(diagnostic.learning.diagnostic);assert.equal(diagnostic.turns.filter(t=>['PrepareSession','FinalReport'].includes(t.action)).length,2);
    await matrix('diagnostic-report',p.getByRole('heading',{name:'Comenzar mi plan',exact:true}));
    await p.getByRole('button',{name:'Ver evidencias y compartir reporte',exact:true}).click();
    await p.getByRole('dialog',{name:'Evidencias y reporte privado',exact:true}).waitFor();
    await capture(p,'learn-report-drawer-1440-dark',1440,true,p.getByRole('dialog',{name:'Evidencias y reporte privado',exact:true}));
    await p.keyboard.press('Escape');await p.getByRole('dialog',{name:'Evidencias y reporte privado',exact:true}).waitFor({state:'hidden'});assert.equal(await p.getByRole('dialog').count(),0);
    assert.equal(await p.getByRole('button',{name:'Ver evidencias y compartir reporte',exact:true}).evaluate(e=>e===document.activeElement),true);
    goalSession=await mutation(p,'Comenzar mi plan',/\/prepared-plan\/start$/);await p.waitForURL(origin+'/tutoring/'+goalSession.id);assert.equal(goalSession.modality,'Prepared');assert.equal(goalSession.prepared.generationState,'Requested');
    await mutation(p,'Preparar tutoría',/\/messages$/);await mutation(p,'Comenzar tutoría preparada',/\/cards$/);await mutation(p,'Dame una pista',/\/cards$/);
    goalSession=await mutation(p,'Pausar recorrido',/\/pause$/);
  });
  await check('LEARN-1 E: private scripts and evidence remain denied to Teacher and Admin',async()=>{
    for(const actor of [teacher,admin])for(const id of sessions)assert.equal((await actor.context.request.get(origin+'/api/tutoring-sessions/'+id)).status(),404);
    const storage=await p.evaluate(()=>({local:Object.keys(localStorage),session:Object.keys(sessionStorage)}));assert.ok(storage.local.every(k=>k==='aulatutor.theme.v1'));assert.deepEqual(storage.session,[]);
    assert.equal(JSON.stringify(diagnostic).includes('REFERENCIA_PRIVADA_MOCK_NO_PROYECTAR'),false);
  });
  await check('LEARN-1 D: own OpenRouter generation and grouped evaluation reserve two physical simulated attempts, local work reserves zero',async()=>{
    await api(student.context,'PUT','/api/ai-connections/selection',{connectionId:byok.studentConnectionId,model:'qa/structured:free',privacyAccepted:true,paidAccepted:false});
    const reserved=async()=> (await api(admin.context,'GET','/api/admin/operations/usage')).providers.reduce((n,x)=>n+x.reservations,0);
    const before=await reserved();let s=await create('Tutoring');s=await mutation(p,'Preparar tutoría',/\/messages$/);assert.equal(s.providerId,'OpenRouter');assert.equal(await reserved(),before+1);
    await api(student.context,'PUT','/api/ai-connections/selection',{connectionId:null,model:null,privacyAccepted:false,paidAccepted:false});
    await mutation(p,'Comenzar tutoría preparada',/\/cards$/);await mutation(p,'Dame una pista',/\/cards$/);s=await play(false);assert.equal(await reserved(),before+1);
    s=await mutation(p,'Solicitar evaluación final con IA',/\/messages$/);assert.equal(s.learning.reports[0].providerId,'OpenRouter');assert.equal(await reserved(),before+2);
    await p.reload();assert.equal(await reserved(),before+2);
  });
  return {status:'passed',tutoringId:tutoring.id,diagnosticId:diagnostic.id,pausedGoalId:goalSession.id,pausedGoalHelpKey:goalSession.turns.find(t=>t.action==='Hint').requestId,
    simulatedHttp:{generation:1,ordinary:0,evaluation:1},logicalCalls:{generation:1,ordinary:0,evaluation:1},scope:'Fictional Mock and PersonalQa only. Logical assertions also in PostgreSQL tests.'};
}

// Iteration 9 extends the existing runner; no new framework or external provider.
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

export async function academicPilot(h) {
  const {api,login,mutation,capture,check,qa,origin,root,browser,admin,student}=h;
  const wire=JSON.parse(await fs.readFile(path.join(root,'docs/pilot/sql-procedures-v2.json'),'utf8'));
  const scenario={teacherEmail:'pilot-teacher@example.invalid',studentEmails:[qa.studentEmail,qa.tutoringOwnerEmail,'pilot-student-c@example.invalid'],classes:{},sessions:[]};
  let teacher,other,group,content;
  async function register(email,role,name){const c=await browser.newContext({ignoreHTTPSErrors:true});try{await api(c,'POST','/api/auth/register',{email,password:qa.password,displayName:name,requestedRole:role});}finally{await c.close();}}
  async function publish(l){for(const action of ['review','approve','publish'])l=await api(teacher.context,'POST',`/api/lessons/${l.id}/${action}`,{requestId:crypto.randomUUID(),revision:l.revision,targetId:action==='publish'?l.versions.at(-1).id:null});return l;}
  async function assign(l){return api(teacher.context,'POST','/api/teacher/assignments/',{requestId:crypto.randomUUID(),lessonVersionId:l.versions.at(-1).id,groupId:group.id});}
  async function save(l,c){return api(teacher.context,'PUT',`/api/lessons/${l.id}/draft`,{revision:l.revision,content:c});}
  async function read(actor,id){return api(actor.context,'GET','/api/tutoring-sessions/'+id);}
  async function local(actor,s,action,text=null){return api(actor.context,'POST',`/api/tutoring-sessions/${s.id}/cards`,{requestId:crypto.randomUUID(),revision:s.revision,action,text,exerciseId:action==='SubmitAttempt'?s.assignedClass.currentExerciseId:null});}
  const answer=(id,weak=false)=>id==='e-create'?'CREATE PROCEDURE':id==='e-parameter'?'@CategoriaId':id==='e-exists'?'Verdadero':weak?'CREATE PROCEDURE dbo.BuscarProductos AS SELECT Nombre FROM dbo.Productos; No sé incluir el parámetro.':wire.lesson.exercises.at(-1).referenceSolution;
  await check('9 A: fictional SQL teacher, three creation paths and exact published assignments',async()=>{
    await register(scenario.teacherEmail,'Teacher','Profesor SQL ficticio');
    const users=await api(admin.context,'GET','/api/admin/users?email=pilot-teacher');
    const pending=(Array.isArray(users)?users:users.items).find(u=>u.email===scenario.teacherEmail);assert.ok(pending);
    await api(admin.context,'POST',`/api/admin/users/${pending.id}/teacher-decision`,{approve:true});
    teacher=await login(scenario.teacherEmail);other=await login(qa.tutoringOwnerEmail);
    await register(scenario.studentEmails[2],'Student','Estudiante C ficticio');
    const cUsers=await api(admin.context,'GET','/api/admin/users?email=pilot-student-c');const c=(Array.isArray(cUsers)?cUsers:cUsers.items).find(u=>u.email===scenario.studentEmails[2]);assert.ok(c);
    const t=await api(teacher.context,'GET','/api/me'),a=await api(student.context,'GET','/api/me'),b=await api(other.context,'GET','/api/me');
    group=await api(admin.context,'POST','/api/groups/',{name:'SQL Server · Piloto interno 9'});scenario.groupId=group.id;
    for(const [u,kind] of [[t,'Teacher'],[a,'Student'],[b,'Student'],[c,'Student']])await api(admin.context,'PUT',`/api/groups/${group.id}/members`,{userId:u.id,kind});
    const preview=await api(teacher.context,'POST','/api/lessons/import/preview',{json:JSON.stringify(wire)});assert.equal(preview.errors.length,0);content=preview.content;
    const p=teacher.page;
    await p.goto(origin+'/teacher/lessons/new/manual');await p.getByLabel('Título',{exact:true}).fill('SQL conversacional · referencia ficticia');
    await p.getByRole('button',{name:'Crear borrador',exact:true}).click();await p.waitForURL(/\/teacher\/lessons\/[a-f0-9-]{36}$/);
    let manual=await api(teacher.context,'GET','/api/lessons/'+p.url().split('/').at(-1));
    manual=await save(manual,{...content,title:'SQL conversacional · referencia ficticia',preloaded:null});await p.reload();
    await p.getByLabel('Descripción',{exact:true}).fill('Referencia SQL ficticia revisada manualmente; el código no se ejecuta.');await mutation(p,'Guardar borrador',/\/draft$/);
    manual=await publish(await api(teacher.context,'GET','/api/lessons/'+manual.id));scenario.classes.conversational=await assign(manual);
    await p.goto(origin+'/teacher/lessons/new/ai');await p.getByLabel('Clase con tarjetas y ayudas preparadas',{exact:true}).check();
    await p.getByLabel('Tema de la clase',{exact:true}).fill('Procedimientos SQL Server del piloto ficticio');await p.getByRole('button',{name:'Generar con IA integrada',exact:true}).click();await p.waitForURL(/mode=ai/);
    await mutation(p,'Aplicar propuesta',/\/apply-proposal$/);let generated=await api(teacher.context,'GET','/api/lessons/'+p.url().split('/').at(-1));
    // Mock is generic. A visible teacher edit supplies the authored SQL reference, not a claim of LLM quality.
    generated=await save(generated,{...content,title:'SQL precargada · referencia ficticia'});await p.reload();await p.getByRole('button',{name:'3. Revisar y publicar',exact:true}).click();
    await mutation(p,'Simular como estudiante',/\/simulations$/);await p.waitForURL(/\/simulations\//);await mutation(p,'Comenzar enseñanza simulada',/\/messages$/);await mutation(p,'Pedir pista',/\/messages$/);await mutation(p,'Detener simulación',/\/stop$/);
    generated=await publish(await api(teacher.context,'GET','/api/lessons/'+generated.id));scenario.classes.preloaded=await assign(generated);
    await p.goto(origin+'/teacher/lessons/new/ai');await p.getByLabel('Clase con tarjetas y ayudas preparadas',{exact:true}).check();await p.getByLabel('Tema de la clase',{exact:true}).fill('Procedimientos SQL Server');
    await p.getByRole('button',{name:'Usar ChatGPT',exact:true}).click();assert.ok((await p.getByLabel('Instrucciones para la herramienta externa').inputValue()).includes('aulatutor-lesson-v2'));
    const importedWire=structuredClone(wire);importedWire.lesson.title='SQL importada V2 · referencia ficticia';await p.getByLabel('Clase generada',{exact:true}).fill(JSON.stringify(importedWire));await mutation(p,'Validar y revisar',/\/import\/preview$/);
    await capture(p,'pilot-external-preview-light-1440',1440,false,p.getByRole('heading',{name:'3. Revisar la clase',exact:true}));
    await p.getByRole('button',{name:'Guardar borrador y abrir constructor',exact:true}).click();await p.waitForURL(/\/teacher\/lessons\/[a-f0-9-]{36}$/);
    const external=await publish(await api(teacher.context,'GET','/api/lessons/'+p.url().split('/').at(-1)));scenario.classes.imported=await assign(external);
    assert.equal(new Set(Object.values(scenario.classes).map(x=>x.lessonVersionId)).size,3);
  });
  let a,b;let aSnapshot,bSnapshot;
  await check('9 B: SQL student A completes cards, few aids and persisted progress with AI route blocked',async()=>{
    const p=student.page;let unexpectedAi=0;await p.route('**/api/tutoring-sessions/*/messages',route=>{unexpectedAi++;return route.fulfill({status:503,contentType:'application/problem+json',body:'{"title":"IA bloqueada para comprobar ejecución local"}'});});
    await p.goto(origin+'/classes');await p.getByRole('link',{name:'SQL precargada · referencia ficticia',exact:true}).click();
    await p.waitForURL(origin+'/classes/'+scenario.classes.preloaded.id);await p.getByRole('heading',{level:1,name:'SQL precargada · referencia ficticia',exact:true}).waitFor();
    await p.getByRole('button',{name:'Iniciar clase',exact:true}).click();await p.waitForURL(/\/tutoring\//);
    const id=p.url().split('/').at(-1);await mutation(p,'Comenzar clase precargada',/\/cards$/);a=await read(student,id);
    for(const width of [320,390,1440])for(const dark of [false,true])await capture(p,`pilot-sql-${width}-${dark?'dark':'light'}`,width,dark,p.getByRole('heading',{name:'Definir una operación reutilizable',exact:true}));
    for(let i=0;i<4;i++){
      const e=a.learning.exercises.find(x=>x.id===a.assignedClass.currentExerciseId);
      if(i===1){await mutation(p,'Dame una pista',/\/cards$/);await mutation(p,'Pausar clase',/\/pause$/);await p.reload();await mutation(p,'Continuar clase',/\/resume$/);}
      if(e.options.length)await p.getByLabel(answer(e.approvedExerciseId),{exact:true}).check();else await p.getByLabel(e.kind==='Code'?'Código o SQL':'Respuesta',{exact:true}).fill(answer(e.approvedExerciseId));
      await mutation(p,'Registrar respuesta',/\/cards$/);if(i<3)await mutation(p,'Siguiente actividad',/\/cards$/);a=await read(student,id);
    }
    await mutation(p,'Finalizar clase precargada',/\/complete$/);a=await read(student,id);assert.equal(unexpectedAi,0);assert.equal(a.learning.attempts.length,4);assert.equal(a.learning.attempts.filter(x=>x.outcome==='NeedsReview').length,1);
    aSnapshot=a.learning.attempts;await p.unroute('**/api/tutoring-sessions/*/messages');scenario.sessions.push(a.id);
  });
  await check('9 C: SQL student B has independent errors, progressive aids, local report and explicit recovery',async()=>{
    b=await api(other.context,'POST',`/api/students/me/classes/${scenario.classes.preloaded.id}/start`,{requestId:crypto.randomUUID()});b=await local(other,b,'Start');
    for(let i=0;i<4;i++){
      const e=b.learning.exercises.find(x=>x.id===b.assignedClass.currentExerciseId);
      if(i<3){for(let n=0;n<(i===0?2:1);n++)b=await local(other,b,'SubmitAttempt',i===0?'EXECUTE':i===1?'int':'Falso');
        for(let n=0;n<(i===1?1:2);n++){b=await local(other,b,'Hint');if(i===0&&n===0)assert.ok(!JSON.stringify(b).includes(wire.preloaded.exercises[0].hints[1]));}
        if(i===1)b=await local(other,b,'Simplify');}
      b=await local(other,b,'SubmitAttempt',answer(e.approvedExerciseId,true));if(i<3)b=await local(other,b,'NextActivity');
    }
    b=await api(other.context,'POST',`/api/tutoring-sessions/${b.id}/complete`,{revision:b.revision});assert.equal(b.learning.attempts.length,8);assert.equal(b.learning.attempts.filter(x=>x.outcome==='Incorrect').length,4);
    assert.equal(a.assignedClass.class.lessonVersionId,b.assignedClass.class.lessonVersionId);assert.equal(a.assignedClass.class.contentHash,b.assignedClass.class.contentHash);
    for(const s of [a,b])for(const key of ['acceptedAnswers','referenceSolution','contextJson'])assert.ok(!JSON.stringify(s).includes('"'+key+'"'));
    bSnapshot=b.learning.attempts;scenario.sessions.push(b.id);const p=other.page;await p.goto(origin+'/tutoring/'+b.id);
    await p.route('**/api/tutoring-sessions/*/messages',route=>route.fulfill({status:503,contentType:'application/problem+json',body:'{"title":"Proveedor indisponible simulado; reporte local conservado"}'}),{times:1});
    await p.getByRole('button',{name:'Solicitar evaluación cualitativa final con IA',exact:true}).click();await p.getByRole('button',{name:'Recuperar misma solicitud',exact:true}).waitFor();assert.equal((await read(other,b.id)).learning.reports.length,1);
    await mutation(p,'Recuperar misma solicitud',/\/messages$/);b=await read(other,b.id);assert.equal(b.learning.reports.length,2);assert.deepEqual(b.learning.attempts,bSnapshot);await p.reload();assert.deepEqual((await read(other,b.id)).learning.reports,b.learning.reports);
    await p.locator('app-report-card').filter({hasText:'preloaded-evaluation-v1'}).getByText('Origen del reporte',{exact:true}).click();
    await capture(p,'pilot-result-dark-390',390,true,p.getByText('preloaded-evaluation-v1',{exact:false}));await capture(p,'pilot-result-light-1440',1440,false,p.getByText('preloaded-evaluation-v1',{exact:false}));
    await student.page.getByRole('button',{name:'Solicitar evaluación cualitativa final con IA',exact:true}).click();await student.page.locator('app-report-card').filter({hasText:'preloaded-evaluation-v1'}).getByText('Origen del reporte',{exact:true}).click();await student.page.getByText('preloaded-evaluation-v1',{exact:false}).waitFor();a=await read(student,a.id);assert.deepEqual(a.learning.attempts,aSnapshot);
  });
  await check('9 D: SQL supervisor sees denominators, attention, references and independent feedback',async()=>{
    const groupView=await api(teacher.context,'GET',`/api/teacher/supervision/assignments/${scenario.classes.preloaded.id}`);assert.equal(groupView.assignment.counts.eligibleStudents,3);assert.equal(groupView.assignment.counts.completed,2);assert.equal(groupView.assignment.counts.notOpened,1);assert.equal(groupView.objectives.reduce((n,o)=>n+o.eligibleRequiredSlots,0),12);assert.equal(groupView.objectives.reduce((n,o)=>n+o.deliveredRequired,0),8);assert.ok(groupView.cases.length>=2);
    const bu=await api(other.context,'GET','/api/me');const individual=await api(teacher.context,'GET',`/api/teacher/supervision/assignments/${scenario.classes.preloaded.id}/students/${bu.id}`);assert.ok(individual.aiObservations.length);assert.ok(individual.aiObservations.every(o=>o.evidenceIds.every(id=>b.learning.attempts.some(e=>e.id===id))));
    const p=teacher.page;await p.goto(`${origin}/teacher/supervision/${scenario.classes.preloaded.id}/students/${bu.id}`);await p.getByLabel('Comentario para el estudiante').fill('Piloto ficticio: revisa el parámetro y explica la separación de lotes. La evaluación IA no aprueba el código.');await mutation(p,'Guardar decisión docente',/\/reviews$/);
    await capture(p,'pilot-supervision-light-1440',1440,false,p.getByText('Piloto ficticio: revisa el parámetro y explica la separación de lotes. La evaluación IA no aprueba el código.',{exact:true}));await capture(p,'pilot-supervision-dark-390',390,true,p.getByText('Piloto ficticio: revisa el parámetro y explica la separación de lotes. La evaluación IA no aprueba el código.',{exact:true}));
    assert.deepEqual((await read(other,b.id)).learning.attempts,bSnapshot);assert.ok((await api(other.context,'GET',`/api/students/me/classes/${scenario.classes.preloaded.id}/feedback`)).length);
    assert.equal((await admin.context.request.get(origin+'/api/tutoring-sessions/'+a.id)).status(),404);assert.equal((await teacher.context.request.get(origin+'/api/tutoring-sessions/'+qa.completedLearningId)).status(),404);
    const foreign=await teacher.context.request.get(origin+'/api/teacher/supervision/?groupId='+qa.foreignGroupId);assert.equal(foreign.status(),404);
    const c=await login(scenario.studentEmails[2]);const available=await api(c.context,'GET','/api/students/me/classes/');assert.equal(available.filter(x=>x.groupName===group.name).length,3);await c.context.close();
  });
  await fs.writeFile(path.join(root,'.cache/pilot-scenario.json'),JSON.stringify(scenario,null,2));
  await teacher.context.close();await other.context.close();
  return {studentsEnrolled:3,studentsCompleted:2,attempts:[4,8],incorrectAttempts:[0,4],requiredOpportunities:12,deliveredRequired:8,
    sessions:scenario.sessions,startingSteps:{fromHome:4,fromClassList:3},note:'Scripted technical walkthrough, not interviews, learning evidence or LLM quality validation.'};
}

import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Session, TutoringService, TurnRequest } from '../tutoring/tutoring.service';
import { SafeResponse } from '../tutoring/safe-response';
import { ReportCard } from '../tutoring/report-card';

@Component({selector:'app-class-cards',imports:[FormsModule,SafeResponse,ReportCard],styleUrl:'./class-cards.css',template:`
@let s=session();@let a=s.assignedClass!;
<header><p class="eyebrow">Clase precargada · Versión {{a.class.versionNumber}}</p><h1>{{s.topic}}</h1><p>{{s.goal}}</p>
<p>{{a.class.teacherName}} · {{a.class.groupName}}</p><p class="note">El docente responsable puede revisar las respuestas, intentos, ayudas y evaluación de esta clase.</p>
@if(a.unavailableReason){<p class="notice warning" role="status">{{a.unavailableReason}}</p>}
<progress class="class-progress" [value]="a.class.deliveredRequired" [max]="a.class.totalRequired||1" [attr.aria-label]="'Actividades obligatorias entregadas: '+a.class.deliveredRequired+' de '+a.class.totalRequired"></progress><p>Obligatorias entregadas: {{a.class.deliveredRequired}} / {{a.class.totalRequired}} · {{s.state==='Completed'?'Finalizada':s.state==='Paused'?'Pausada':'Tu recorrido'}}</p>
</header>

@if(error()){<p class="notice danger" role="alert">{{error()}}</p>}
<div class="cards-layout"><section class="exercise-card" aria-labelledby="card-title">
<h2 id="card-title">{{a.sectionTitle}}</h2><details class="activity-material"><summary>Material de esta actividad</summary><app-safe-response [text]="a.sectionContent"/></details>
@if(exercise();as e){@if(objective(e.approvedObjectiveId)!==s.goal){<p><strong>Objetivo:</strong> {{objective(e.approvedObjectiveId)}}</p>}<p>Actividad {{e.order+1}} · {{e.required?'Obligatoria':'Opcional'}}</p><div class="activity-prompt"><app-safe-response [text]="e.prompt"/></div>
@if(s.state==='Active'){
<form (ngSubmit)="act('SubmitAttempt')"><fieldset [disabled]="blocked()"><legend>Tu respuesta</legend>
@if(e.options.length){@for(o of e.options;track $index){<label class="option"><input type="radio" name="answer" [value]="o" [(ngModel)]="answer"/>{{o}}</label>}}
@else{<label for="card-answer">{{e.kind==='Code'?'Código o SQL':'Respuesta'}}</label><textarea id="card-answer" name="answer" [(ngModel)]="answer" maxlength="2000" rows="5"></textarea>}
<button type="submit" [disabled]="!answer.trim()">Registrar respuesta</button></fieldset></form>
<button type="button" class="secondary" (click)="act('NextActivity')" [disabled]="blocked()">Siguiente actividad</button>}
}
@if(s.state==='Created'){<button type="button" (click)="act('Start')" [disabled]="blocked()">Comenzar clase precargada</button>}
@for(m of feedback();track m.id){<section class="notice info"><app-safe-response [text]="m.text"/></section>}
@if(s.state==='Active'){<div class="actions"><button type="button" class="secondary" (click)="transition('pause')" [disabled]="blocked()">Pausar clase</button><button type="button" (click)="transition('complete')" [disabled]="blocked()||!a.canComplete">Finalizar clase precargada</button></div>}
@if(s.state==='Paused'){<button type="button" (click)="transition('resume')" [disabled]="blocked()">Continuar clase</button>}
@if(s.state==='Completed'){<h2>Tu resultado</h2>@if(!s.learning.reports.length){<app-safe-response [text]="s.summary??''"/>}
@for(r of s.learning.reports;track r.id){<app-report-card [report]="r"/>}
<p>Puedes solicitar una evaluación adicional con IA.</p>
@if(!evaluated()){<button type="button" (click)="act('FinalReport')" [disabled]="blocked()">Solicitar evaluación cualitativa final con IA</button>}
}
</section><aside aria-label="Ayudas de la actividad"><details open><summary>Pistas y explicaciones</summary>
@if(s.state==='Active'&&exercise()){<button type="button" (click)="act('Hint')" [disabled]="blocked()">Dame una pista</button><button type="button" class="secondary" (click)="act('Simplify')" [disabled]="blocked()">Otra explicación</button>}
@for(m of helps();track m.id){<article><app-safe-response [text]="m.text"/></article>}
</details>@for(m of introductions();track m.id){<details class="class-introduction"><summary>Introducción de la clase</summary><app-safe-response [text]="m.text"/></details>}<button type="button" class="secondary" (click)="refresh.emit()">Consultar estado</button>
@if(uncertain()){<p role="status">No se confirmó el envío; conservamos tu respuesta.</p><button type="button" (click)="recover()" [disabled]="busy()">Recuperar misma solicitud</button>}
@if(s.pendingRequestId;as key){@for(t of s.turns;track t.requestId){@if(t.requestId===key){<p role="status">{{t.error||'Evaluación pendiente. Puedes consultar el resultado local.'}}</p>@if(t.canRetry){<button type="button" (click)="act('FinalReport',true)" [disabled]="busy()">Reintentar evaluación</button>}<button type="button" class="secondary" (click)="cancel()" [disabled]="busy()">Cancelar evaluación pendiente</button>}}}
</aside></div><p role="status">{{busy()?'Guardando…':''}}</p>`})
export class ClassCards {
 readonly session=input.required<Session>();readonly updated=output<Session>();readonly refresh=output<void>();readonly busy=signal(false);readonly error=signal('');readonly uncertain=signal<TurnRequest|null>(null);
 private readonly api=inject(TutoringService);private readonly destroy=inject(DestroyRef);answer='';
 exercise(){const s=this.session();return s.learning.exercises.find(e=>e.id===s.assignedClass?.currentExerciseId);}
 objective(id:string|null){return this.session().assignedClass?.class.objectives.find(o=>o.id===id)?.text??'';}
 evaluated(){return this.session().learning.reports.some(r=>r.promptVersion==='preloaded-evaluation-v1'&&!r.outdated);}
 blocked(){return this.busy()||!!this.uncertain()||!!this.session().pendingRequestId||!this.session().assignedClass?.class.available;}
 private messages(actions:string[]){const s=this.session(),id=this.exercise()?.id;const keys=s.turns.filter(t=>t.exerciseId===id&&actions.includes(t.action)).map(t=>t.requestId);return s.messages.filter(m=>keys.includes(m.requestId));}
 helps(){return this.messages(['Hint','Simplify']);}feedback(){return this.messages(['SubmitAttempt']).slice(-1);}
 introductions(){const s=this.session();const keys=s.turns.filter(t=>t.action==='Start').map(t=>t.requestId);return s.messages.filter(m=>keys.includes(m.requestId));}
 act(action:string,retry=false){if(this.busy())return;const s=this.session();const t=s.turns.find(t=>t.requestId===s.pendingRequestId);if(retry&&!t?.canRetry)return;this.dispatch({requestId:retry?t!.requestId:crypto.randomUUID(),revision:s.revision,action,text:action==='SubmitAttempt'?this.answer:null,retry,exerciseId:action==='SubmitAttempt'?this.exercise()?.id:null});}
 recover(){const r=this.uncertain();if(r&&!this.busy())this.dispatch(r);}
 private dispatch(r:TurnRequest){this.busy.set(true);this.error.set('');const s=this.session();const call=r.action==='FinalReport'?this.api.send(s.id,r):this.api.card(s.id,r);call.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>{this.updated.emit(v);this.busy.set(false);this.uncertain.set(null);if(r.action==='NextActivity')this.answer='';},error:e=>{this.busy.set(false);this.error.set(e.error?.title??'No se confirmó el envío. Consulta el estado o recupera la misma solicitud.');if(e.status===0||e.status>=500)this.uncertain.set(r);}});}
 transition(action:string){if(this.blocked())return;this.busy.set(true);this.api.transition(this.session().id,action,this.session().revision).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>{this.updated.emit(s);this.busy.set(false);},error:e=>{this.error.set(e.error?.title??'No se confirmó el cambio. Consulta el estado.');this.busy.set(false);}});}
 cancel(){const s=this.session();if(!s.pendingRequestId)return;this.api.cancel(s.id,s.pendingRequestId,s.revision).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>this.updated.emit(v),error:()=>this.error.set('No se pudo cancelar; consulta el estado.')});}
}

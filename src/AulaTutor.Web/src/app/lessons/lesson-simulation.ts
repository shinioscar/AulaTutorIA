import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subscription } from 'rxjs';
import { LessonService, SimulationView, LessonOperation, lessonState, outcomeLabel, exerciseKind, lessonAction } from './lesson.service';
import { SafeResponse } from '../tutoring/safe-response';
import { DatePipe } from '@angular/common';
import { aiErrorMessage } from '../ai/ai-error-message';
@Component({selector:'app-lesson-simulation',imports:[FormsModule,RouterLink,SafeResponse,DatePipe],styleUrl:'./lessons.css',template:`
<a [routerLink]="['/teacher/lessons',id]">← Volver al editor</a><h1>Simulación como estudiante</h1><p class="notice info">Practica como estudiante con la revisión seleccionada de tu clase.</p>
@if(error()){<p role="alert" class="notice danger">{{error()}}</p>}
@if(sim();as s){<section><h2>{{s.title}}</h2><p>Revisión {{s.draftRevision}} · {{state(s.state)}} · Sección {{sectionNumber(s)}} de {{s.sectionCount}}</p>
<div class="actions"><button class="secondary" (click)="refresh()">Consultar estado</button>@if(s.state==='Active'){<button class="secondary" (click)="command('stop')">Detener simulación</button>}@else{<button (click)="command('resume')" [disabled]="busy()">Reanudar simulación guardada</button>}</div>
<h3>{{s.sectionTitle}}</h3><app-safe-response [text]="s.sectionContent"/>
@if(!s.operations.length){<button (click)="send('Start')" [disabled]="busy()||s.state!=='Active'">Comenzar enseñanza simulada</button>}
<div class="chat" role="log" aria-label="Conversación de simulación" tabindex="0">@for(o of s.operations;track o.requestId){<article><p class="teacher-message"><strong>Docente como estudiante · {{actionLabel(o.action)}}</strong><br/>{{o.text}}</p>@if(o.reply){<p><strong>Tutor</strong></p><app-safe-response [text]="o.reply"/>}@if(o.outcome){<p class="notice info">{{outcome(o.outcome)}} · {{o.validationType==='Deterministic'?'Comparador determinista':'Resultado orientativo'}}</p>}
<p>{{state(o.state)}}</p>@if(o.errorCode){<p class="notice warning">{{aiError(o.errorCode)}} Respuesta conservada.</p>}@if(o.state==='Failed'||o.errorCode==='Interrupted'){@if(o.retryNotBefore&&!retryReady(o)){<p>Podrás reintentar desde {{o.retryNotBefore|date:'short'}}.</p>}<button class="secondary" (click)="retry(o)" [disabled]="busy()||!retryReady(o)||s.state!=='Active'">Reintentar turno</button>}@if(o.state==='Pending'||o.state==='Failed'){<button class="secondary" (click)="command('cancel',o.requestId)">Cancelar turno</button>}</article>}</div>
@if(s.exercise;as e){<article><h3>Actividad actual · {{kind(e.kind)}}</h3><app-safe-response [text]="e.prompt"/>@if(e.options.length){<ul>@for(option of e.options;track $index){<li>{{option}}</li>}</ul>}
@if(s.answered){@if(lastAnswer(s);as a){<p class="notice info" role="status">{{outcome(a.outcome||'')}} · {{a.validationType==='Deterministic'?'Comparador determinista':'Resultado orientativo'}}</p>@if(a.reply){<app-safe-response [text]="a.reply"/>}}}
<form (ngSubmit)="send('Answer',answer)"><label for="simulation-answer">Respuesta de prueba (correcta, incorrecta o abierta)</label><textarea id="simulation-answer" name="answer" maxlength="2000" [(ngModel)]="answer"></textarea><button type="submit" [disabled]="blocked()||!answer.trim()">Registrar respuesta simulada</button></form></article>}
@if(!s.preloaded){<form (ngSubmit)="send('Message',text)"><label for="simulation-message">Duda o mensaje libre</label><textarea id="simulation-message" name="text" maxlength="2000" [(ngModel)]="text"></textarea><button type="submit" [disabled]="blocked()||!text.trim()">Enviar duda</button></form>}
<div class="actions"><button class="secondary" (click)="send('Hint')" [disabled]="blocked()">Pedir pista</button><button class="secondary" (click)="send('Simplify')" [disabled]="blocked()">Simplificar explicación</button><button (click)="send('Next')" [disabled]="blocked()||s.sectionIndex>=s.sectionCount">Continuar actividad</button></div>
<p class="note">Reintentar puede consumir cuota adicional del proveedor.</p>
</section>}@else{<p role="status">Cargando simulación…</p>}
`})
export class LessonSimulation {
  private readonly service=inject(LessonService);private readonly route=inject(ActivatedRoute);private readonly destroy=inject(DestroyRef);
  readonly sim=signal<SimulationView|null>(null);readonly error=signal('');readonly busy=signal(false);readonly state=lessonState;readonly outcome=outcomeLabel;
  readonly id=this.route.snapshot.paramMap.get('id')??'';private readonly simId=this.route.snapshot.paramMap.get('simulationId')??'';
  answer='';text='';private request:Subscription|null=null;private polling=false;private timer:ReturnType<typeof setInterval>;
  readonly kind=exerciseKind;readonly actionLabel=lessonAction;
  readonly aiError=aiErrorMessage;readonly now=signal(Date.now());
  lastAnswer(s:SimulationView){return [...s.operations].reverse().find(o=>o.action==='Answer');}
  sectionNumber(s:SimulationView){return Math.min(s.sectionIndex+1,s.sectionCount);}
  constructor(){this.refresh();this.timer=setInterval(()=>{this.now.set(Date.now());if((this.busy()||this.sim()?.operations.some(o=>o.isGenerating))&&!this.polling){this.polling=true;this.service.simulation(this.id,this.simId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>{this.accept(s);this.polling=false;},error:()=>{this.polling=false;}});}},2000);this.destroy.onDestroy(()=>clearInterval(this.timer));}
  blocked(){return this.busy()||this.sim()?.state!=='Active'||!this.sim()?.operations.length||this.sim()?.operations.some(o=>o.state==='Pending'||o.state==='Failed');}
  private accept(s:SimulationView){if(s.revision>=(this.sim()?.revision??-1))this.sim.set(s);}
  private run(obs:Observable<SimulationView>,clear?:string){this.busy.set(true);this.error.set('');this.request=obs.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>{this.accept(s);this.busy.set(false);if(clear==='Answer')this.answer='';if(clear==='Message')this.text='';},error:e=>{this.busy.set(false);this.error.set(e.error?.title??'No se completó el turno. Tus campos están conservados; consulta el estado.');}});}
  refresh(){this.service.simulation(this.id,this.simId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>this.accept(s),error:e=>this.error.set(e.error?.title??'Simulación no disponible.')});}
  send(action:string,text:string|null=null){const s=this.sim();if(s&&!this.busy())this.run(this.service.turn(this.id,this.simId,crypto.randomUUID(),s.revision,action,text),action);}
  retryReady(o:LessonOperation){return o.attempts<3&&(!o.retryNotBefore||Date.parse(o.retryNotBefore)<=this.now());}
  retry(o:LessonOperation){const s=this.sim();if(s&&this.retryReady(o))this.run(this.service.turn(this.id,this.simId,o.requestId,s.revision,o.action,o.text||null,true));}
  command(action:string,targetId:string|null=null){if(!this.sim())return;this.service.simulation(this.id,this.simId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>{this.accept(s);this.run(this.service.simulationCommand(this.id,this.simId,action,s.revision,targetId));},error:e=>this.error.set(e.error?.title??'No se pudo consultar el estado.')});}
}

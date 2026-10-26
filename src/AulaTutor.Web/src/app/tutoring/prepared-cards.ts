import { Component, DestroyRef, OnChanges, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { Session, TutoringService, TurnRequest } from './tutoring.service';
import { SafeResponse } from './safe-response';
import { LearningPanel } from './learning-panel';
import { DetailDrawer } from '../shared/detail-drawer';

@Component({selector:'app-prepared-cards',imports:[FormsModule,RouterLink,SafeResponse,LearningPanel,DetailDrawer],
  templateUrl:'./prepared-cards.html',styleUrls:['../lessons/class-cards.css','./prepared-cards.css']})
export class PreparedCards implements OnChanges {
  readonly session=input.required<Session>();readonly updated=output<Session>();readonly refresh=output<void>();
  readonly working=output<boolean>();private generation:Subscription|null=null;
  readonly busy=signal(false);readonly error=signal('');readonly uncertain=signal<TurnRequest|null>(null);
  readonly resultsOpen=signal(false);
  showResults(){this.refresh.emit();this.resultsOpen.set(true);}
  private readonly api=inject(TutoringService);private readonly destroy=inject(DestroyRef);private readonly router=inject(Router);
  answer='';objectiveOrder=0;planModality='Prepared';planLength='Short';private lastExercise='';private planKey:string|null=null;
  ngOnChanges(){const id=this.exercise()?.id??'';if(id!==this.lastExercise){this.answer='';this.lastExercise=id;}}
  diagnostic(){return this.session().experience==='Diagnostic';}
  exercise(){return this.session().learning.exercises.find(e=>e.id===this.session().prepared?.currentExerciseId);}
  attempts(){return this.session().learning.attempts.filter(a=>a.exerciseId===this.exercise()?.id);}
  evaluated(){return this.session().learning.reports.some(r=>r.promptVersion==='prepared-evaluation-v1');}
  blocked(){return this.busy()||!!this.uncertain()||!!this.session().pendingRequestId;}
  feedback(){const s=this.session(),id=this.exercise()?.id;const keys=s.turns.filter(t=>t.exerciseId===id&&['Hint','Simplify','SubmitAttempt'].includes(t.action)).map(t=>t.requestId);return s.messages.filter(m=>keys.includes(m.requestId));}
  act(action:string,retry=false){if(this.busy())return;const s=this.session(),t=s.turns.find(t=>t.requestId===s.pendingRequestId);if(retry&&!t?.canRetry)return;
    this.dispatch({requestId:retry?t!.requestId:crypto.randomUUID(),revision:s.revision,action,text:action==='SubmitAttempt'?this.answer:null,retry,exerciseId:action==='SubmitAttempt'?this.exercise()?.id:null});}
  recover(){const r=this.uncertain();if(r&&!this.busy())this.dispatch(r);}
  private dispatch(r:TurnRequest){this.busy.set(true);this.working.emit(true);this.error.set('');this.uncertain.set(r);const s=this.session();
    const call=['PrepareSession','FinalReport'].includes(r.action)?this.api.send(s.id,r):this.api.card(s.id,r);
    this.generation=call.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>{this.updated.emit(v);this.busy.set(false);this.working.emit(false);this.uncertain.set(null);if(r.action==='SubmitAttempt')this.answer='';},error:e=>{this.busy.set(false);this.working.emit(false);this.error.set(e.error?.title??(e.status===429?'Se alcanzó el límite de solicitudes. Espera antes de volver a solicitar IA. Tus respuestas y resultados se conservan.':'No se confirmó el envío. Consulta el estado o recupera la misma solicitud.'));if(e.status>0&&e.status<500)this.uncertain.set(null);this.refresh.emit();}});}
  transition(action:string){if(this.blocked())return;this.busy.set(true);this.error.set('');this.api.transition(this.session().id,action,this.session().revision).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>{this.updated.emit(v);this.busy.set(false);},error:e=>{this.busy.set(false);this.error.set(e.error?.title??'No se confirmó el cambio. Consulta el estado.');this.refresh.emit();}});}
  cancel(){const s=this.session();if(!s.pendingRequestId)return;this.generation?.unsubscribe();this.api.cancel(s.id,s.pendingRequestId,s.revision).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>{this.updated.emit(v);this.uncertain.set(null);this.busy.set(false);this.working.emit(false);},error:()=>{this.busy.set(false);this.working.emit(false);this.error.set('No se pudo confirmar la cancelación. Consulta el estado.');this.refresh.emit();}});}
  startPlan(){const s=this.session(),p=s.learning.plan;if(!p||this.blocked())return;this.busy.set(true);this.error.set('');this.planKey??=crypto.randomUUID();
    this.api.startPreparedPlan(s.id,{requestId:this.planKey,planId:p.id,objectiveOrder:Number(this.objectiveOrder),modality:this.planModality,length:this.planLength}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:v=>{void this.router.navigate(['/tutoring',v.id]);this.busy.set(false);},error:e=>{this.busy.set(false);this.error.set(e.error?.title??'No se confirmó el inicio del plan. Repite la misma selección para recuperarlo.');}});}
}

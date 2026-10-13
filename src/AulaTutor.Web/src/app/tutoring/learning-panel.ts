import { Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Session, LearningAction, TutoringService, Report } from './tutoring.service';
import { ReportCard } from './report-card';
@Component({selector:'app-learning-panel',standalone:true,imports:[FormsModule,ReportCard],templateUrl:'./learning-panel.html',styleUrl:'./learning-panel.css'})
export class LearningPanel implements OnChanges {
  readonly session=input.required<Session>(); readonly disabled=input(false);
  readonly action=output<LearningAction>();readonly changed=output<void>();
  private readonly api=inject(TutoringService);private readonly router=inject(Router);
  readonly localBusy=signal(false);readonly error=signal('');readonly free=signal(false);
  answer='';adjustment='';recipient='';consent=false;readonly recipients=signal<{id:string;displayName:string}[]|null>(null);
  private shareKey:string|null=null;private reinforcementKeys=new Map<string,string>();
  private previousExercise='';private previousAttempts=0;
  readonly exercise=computed(()=>{
    const l=this.session().learning;const plan=l.plan;
    if(plan?.state==='Active')return l.exercises.find(e=>!e.isDiagnostic&&e.order===plan.currentActivity&&!e.completed);
    return [...l.exercises].reverse().find(e=>!e.completed&&(!e.isDiagnostic||!l.diagnostic));
  });
  readonly attempts=computed(()=>this.session().learning.attempts.filter(a=>a.exerciseId===this.exercise()?.id));
  readonly report=computed<Report|undefined>(()=>this.session().learning.reports[0]);
  emit(action:string,text?:string,exerciseId?:string):void {if(!this.disabled()&&!this.localBusy()){this.error.set('');this.action.emit({action,text,exerciseId});}}
  ngOnChanges():void {const id=this.exercise()?.id??'';const count=this.attempts().length;if(id!==this.previousExercise||count>this.previousAttempts)this.answer='';this.previousExercise=id;this.previousAttempts=count;}
  submit():void {const e=this.exercise();if(e&&this.answer.trim())this.emit('SubmitAttempt',this.answer,e.id);}
  outcome(value:string):string{return ({CorrectWithoutHelp:'Correcto sin ayuda',CorrectWithHelp:'Correcto con ayuda',Incorrect:'Incorrecto',NeedsReview:'Requiere revisión · no verificado',NotEvaluable:'No evaluable'} as Record<string,string>)[value]??value;}
  async loadRecipients():Promise<void>{this.localBusy.set(true);try{this.recipients.set(await firstValueFrom(this.api.recipients(this.session().id)));}catch{this.error.set('No se pudieron consultar los docentes autorizados.');}finally{this.localBusy.set(false);}}
  async share():Promise<void>{const r=this.report();if(!r||!this.recipient||!this.consent||this.disabled())return;this.localBusy.set(true);this.shareKey??=crypto.randomUUID();try{await firstValueFrom(this.api.share(this.session().id,r.id,this.recipient,this.shareKey));this.shareKey=null;this.consent=false;this.changed.emit();}catch{this.error.set('No se pudo confirmar la autorización. Reintenta con el mismo reporte y destinatario o actualiza.');}finally{this.localBusy.set(false);}}
  async revoke(id:string):Promise<void>{this.localBusy.set(true);try{await firstValueFrom(this.api.revoke(this.session().id,id));this.changed.emit();}catch{this.error.set('No se pudo confirmar la revocación; actualiza antes de continuar.');}finally{this.localBusy.set(false);}}
  async reinforce(id:string):Promise<void>{this.localBusy.set(true);let key=this.reinforcementKeys.get(id);if(!key){key=crypto.randomUUID();this.reinforcementKeys.set(id,key);}try{const s=await firstValueFrom(this.api.reinforce(this.session().id,id,key));await this.router.navigate(['/tutoring',s.id]);}catch{this.error.set('No se pudo confirmar el refuerzo. Puedes reintentar sin duplicar la solicitud.');}finally{this.localBusy.set(false);}}
}

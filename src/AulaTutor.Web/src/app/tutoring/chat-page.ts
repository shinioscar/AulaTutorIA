import { Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, Subscription } from 'rxjs';
import { Session, Turn, TurnRequest, TutoringService, LearningAction } from './tutoring.service';
import { LearningPanel } from './learning-panel';
import { SafeResponse } from './safe-response';
import { ClassCards } from '../lessons/class-cards';
import { PreparedCards } from './prepared-cards';
const loadErrorMessage='No se pudo cargar la tutoría. Comprueba tu sesión y vuelve a actualizar.';

@Component({standalone:true,imports:[FormsModule,RouterLink,DatePipe,SafeResponse,LearningPanel,ClassCards,PreparedCards],templateUrl:'./chat-page.html',styleUrl:'./chat-page.css'})
export class ChatPage implements OnDestroy {
  cardsUpdated(s:Session){this.adopt(s);this.schedulePoll();}
  cardsWorking(value:boolean){this.busy.set(value);this.schedulePoll();}
  private readonly api=inject(TutoringService); private readonly route=inject(ActivatedRoute);
  readonly session=signal<Session|null>(null); readonly busy=signal(false); readonly message=signal('');
  draft=''; private id=''; private generation:Subscription|null=null; private poll:ReturnType<typeof setTimeout>|null=null;
  readonly uncertain=signal<TurnRequest|null>(null); private destroyed=false;
  @ViewChild('thread') private thread?:ElementRef<HTMLElement>;
  private readonly routeSubscription=this.route.paramMap.subscribe(params=>{
    this.generation?.unsubscribe();if(this.poll)clearTimeout(this.poll);
    this.id=params.get('id')??'';this.session.set(null);this.busy.set(false);this.message.set('');this.uncertain.set(null);this.draft='';void this.refresh();
  });
  get pending():Turn|undefined {const session=this.session();return session?.turns.find(turn=>turn.requestId===session.pendingRequestId);}
  get canSend():boolean {return !this.busy()&&!this.uncertain()&&this.session()?.state==='Active'&&!this.session()?.pendingRequestId&&(this.session()?.assignedClass?.class.available??true);}
  label(state:string):string {return ({Created:'Por iniciar',Active:'Activa',Paused:'Pausada',Completed:'Finalizada'} as Record<string,string>)[state]??state;}
  async refresh():Promise<void> {
    if(this.destroyed)return;
    const requestedId=this.id;
    try {
      const session=await firstValueFrom(this.api.get(requestedId));if(this.destroyed||requestedId!==this.id)return;
      if(!this.adopt(session)){this.schedulePoll();return;}
      if(this.message()===loadErrorMessage)this.message.set('');
      const uncertain=this.uncertain();if(uncertain&&session.turns.some(t=>t.requestId===uncertain.requestId)){this.uncertain.set(null);this.draft='';}
      this.schedulePoll();
    } catch {if(!this.destroyed&&requestedId===this.id)this.message.set(loadErrorMessage);}
  }
  private adopt(session:Session,forceScroll=false):boolean {
    if(session.id!==this.id||session.revision<(this.session()?.revision??-1))return false;
    const element=this.thread?.nativeElement;
    const nearBottom=!element||element.scrollHeight-element.scrollTop-element.clientHeight<80;
    const changed=this.session()?.messages.length!==session.messages.length;
    this.session.set(session);
    if(forceScroll||(changed&&nearBottom))requestAnimationFrame(()=>{
      if(!this.destroyed&&this.thread)this.thread.nativeElement.scrollTop=this.thread.nativeElement.scrollHeight;
    });
    return true;
  }
  private schedulePoll():void {
    if(this.poll)clearTimeout(this.poll);
    if(!this.destroyed&&(this.pending?.isGenerating||this.busy()))this.poll=setTimeout(()=>void this.refresh(),2000);
    else if(!this.destroyed&&this.pending?.retryNotBefore&&this.pending.attempts<3){
      const wait=Date.parse(this.pending.retryNotBefore)-Date.now();
      // Refresh server eligibility once the cooldown expires; never resend to the provider.
      if(wait>0)this.poll=setTimeout(()=>void this.refresh(),Math.min(wait+250,2_147_483_647));
    }
  }
  send(action:string,retry=false):void {
    const session=this.session();if(!session||this.busy())return;
    let request:TurnRequest;
    if(retry){
      const pending=this.pending;if(!pending?.canRetry)return;
      const original=session.messages.find(m=>m.requestId===pending.requestId&&m.role==='Student');
      request={requestId:pending.requestId,revision:session.revision,action:pending.action,text:pending.retryText??(pending.action==='Message'?original?.text??null:null),retry:true,exerciseId:pending.exerciseId};
    } else {
      if(this.uncertain()||session.pendingRequestId)return;
      if(action!=='Start'&&!this.canSend)return;
      if(action==='Message'&&(!this.draft.trim()||this.draft.length>2000))return;
      request={requestId:crypto.randomUUID(),revision:session.revision,action,text:action==='Message'?this.draft:null,retry:false};
    }
    this.dispatch(request);
  }
  recover():void {const request=this.uncertain();if(request&&!this.busy())this.dispatch(request);}
  learningAction(event:LearningAction):void {
    const session=this.session();if(!session||this.busy()||session.pendingRequestId||this.uncertain())return;
    this.dispatch({requestId:crypto.randomUUID(),revision:session.revision,action:event.action,text:event.text??null,retry:false,exerciseId:event.exerciseId??null});
  }
  private dispatch(request:TurnRequest):void {
    const targetId=this.id;
    this.busy.set(true);this.message.set('');this.uncertain.set(request);this.schedulePoll();
    this.generation=this.api.send(targetId,request).subscribe({
      next:session=>{if(targetId!==this.id)return;this.adopt(session,true);this.uncertain.set(null);this.draft='';this.busy.set(false);this.schedulePoll();},
      error:error=>{if(targetId!==this.id)return;this.busy.set(false);if([400,401,403,404,409,429].includes(error.status))this.uncertain.set(null);this.message.set(error.status===429?'Se alcanzó el límite de turnos. Espera antes de volver a enviar.':error.error?.title??'No se pudo confirmar el envío. El intento puede estar guardado: actualiza el estado antes de reintentar.');void this.refresh();}
    });
  }
  async transition(action:string):Promise<void> {
    const session=this.session();if(!session||this.busy())return;
    if(action==='complete'&&!window.confirm('¿Finalizar esta tutoría? Podrás leerla, pero no añadir nuevos turnos.'))return;
    const targetId=this.id;
    this.busy.set(true);this.message.set('');
    try{this.adopt(await firstValueFrom(this.api.transition(targetId,action,session.revision)));}
    catch(error:unknown){if(targetId===this.id){this.message.set((error as {error?:{title?:string}})?.error?.title??'No se pudo cambiar el estado. Actualiza la tutoría para comprobar la revisión y el turno pendiente.');await this.refresh();}}
    finally{if(targetId===this.id){this.busy.set(false);this.schedulePoll();}}
  }
  async cancel():Promise<void> {
    const session=this.session();const pending=this.pending;if(!session||!pending)return;
    const targetId=this.id;
    this.generation?.unsubscribe();this.busy.set(true);this.uncertain.set(null);this.message.set('');
    try{this.adopt(await firstValueFrom(this.api.cancel(targetId,pending.requestId,session.revision)));}
    catch{if(targetId===this.id){this.message.set('No se pudo confirmar la cancelación. Actualiza el estado antes de continuar.');await this.refresh();}}
    finally{if(targetId===this.id){this.busy.set(false);this.schedulePoll();}}
  }
  ngOnDestroy():void {this.destroyed=true;this.routeSubscription.unsubscribe();this.generation?.unsubscribe();if(this.poll)clearTimeout(this.poll);}
}

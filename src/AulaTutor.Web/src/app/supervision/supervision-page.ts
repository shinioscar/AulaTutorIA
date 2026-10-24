import { Component, HostListener, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SafeResponse } from '../tutoring/safe-response';
import { ReviewHistory } from './review-history';
import { SupervisionCharts } from './supervision-charts';
import { DetailDrawer } from '../shared/detail-drawer';
import { ClassSupervision, Dashboard, IndividualSupervision, ReviewRequest, SupervisionService, supervisionLabel } from './supervision.service';

@Component({imports:[RouterLink,FormsModule,DatePipe,SafeResponse,ReviewHistory,SupervisionCharts,DetailDrawer],templateUrl:'./supervision-page.html',styleUrl:'./supervision.css'})
export class SupervisionPage implements OnDestroy {
  private readonly api=inject(SupervisionService);private readonly route=inject(ActivatedRoute);
  readonly dashboard=signal<Dashboard|null>(null);readonly results=signal<ClassSupervision|null>(null);readonly individual=signal<IndividualSupervision|null>(null);
  readonly busy=signal(false);readonly error=signal('');readonly saved=signal('');readonly label=supervisionLabel;
  readonly assignmentState=(state:string)=>({Active:'Activa',Inactive:'Inactiva',Withdrawn:'Retirada'} as Record<string,string>)[state]??state;
  groupId='';tab='students';showResolved=false;target='';state='InReview';attention='InProgress';outcome='NotAssessed';flagged=false;comment='';observation='';recommendation='';evidence:string[]=[];
  private assignmentId='';private studentId='';private epoch=0;private destroyed=false;private pending:ReviewRequest|null=null;
  readonly uncertain=signal(false);
  readonly reviewDirty=signal(false);
  hasUnsavedChanges(){return this.reviewDirty()||this.uncertain();}
  @HostListener('window:beforeunload',['$event']) beforeUnload(event:BeforeUnloadEvent){if(this.hasUnsavedChanges())event.preventDefault();}
  readonly drawerOpen=signal(false);readonly drawerBusy=signal(false);readonly drawerError=signal('');readonly drawerStudent=signal<IndividualSupervision|null>(null);
  drawerTab='summary';studentSearch='';studentState='';studentPage=1;
  private drawerEpoch=0;
  students(){const q=this.studentSearch.trim().toLocaleLowerCase('es');return(this.results()?.students??[]).filter(s=>(!q||s.name.toLocaleLowerCase('es').includes(q))&&(!this.studentState||s.state===this.studentState));}
  closeDrawer(){this.drawerEpoch++;this.drawerOpen.set(false);this.drawerStudent.set(null);this.drawerBusy.set(false);}
  async openStudent(studentId:string){const assignment=this.results()?.assignment.assignmentId;if(!assignment)return;const epoch=++this.drawerEpoch;this.drawerStudent.set(null);this.drawerError.set('');this.drawerTab='summary';this.drawerBusy.set(true);this.drawerOpen.set(true);
    try{const data=await firstValueFrom(this.api.individual(assignment,studentId));if(!this.destroyed&&epoch===this.drawerEpoch)this.drawerStudent.set(data);}
    catch(e:unknown){if(!this.destroyed&&epoch===this.drawerEpoch)this.drawerError.set(this.message(e));}
    finally{if(!this.destroyed&&epoch===this.drawerEpoch)this.drawerBusy.set(false);}
  }
  private readonly sub=this.route.paramMap.subscribe(p=>{this.assignmentId=p.get('id')??'';this.studentId=p.get('studentId')??'';this.dashboard.set(null);this.results.set(null);this.individual.set(null);this.reset();void this.load();});
  async load(){this.closeDrawer();const epoch=++this.epoch;this.busy.set(true);this.error.set('');try{
    if(this.studentId){const v=await firstValueFrom(this.api.individual(this.assignmentId,this.studentId));if(this.live(epoch))this.individual.set(v);}
    else if(this.assignmentId){const v=await firstValueFrom(this.api.class(this.assignmentId));if(this.live(epoch))this.results.set(v);}
    else{const v=await firstValueFrom(this.api.dashboard(this.groupId));if(this.live(epoch))this.dashboard.set(v);}
  }catch(e:unknown){if(this.live(epoch)){this.dashboard.set(null);this.results.set(null);this.individual.set(null);this.error.set(this.message(e));}}finally{if(this.live(epoch))this.busy.set(false);}}
  private live(epoch:number){return !this.destroyed&&epoch===this.epoch;}
  private message(e:unknown){return(e as {error?:{title?:string}})?.error?.title??'No se confirmó la operación. Comprueba tu conexión y relación académica.';}
  reset(){this.reviewDirty.set(false);this.target='';this.state='InReview';this.attention='InProgress';this.outcome='NotAssessed';this.flagged=false;this.comment='';this.observation='';this.recommendation='';this.evidence=[];this.pending=null;this.uncertain.set(false);this.saved.set('');}
  selectTarget(){const review=this.individual()?.reviews.find(r=>(r.exerciseId??'')===this.target);const h=review?.history[0];this.evidence=[];this.state=h?.state??'InReview';this.attention=h?.attention??'InProgress';this.outcome=h?.outcome??'NotAssessed';this.flagged=h?.flagged??false;}
  availableAttempts(){const d=this.individual();return(d?.exercises??[]).filter(e=>!this.target||e.id===this.target).flatMap(e=>e.attempts);}
  visibleCases(){return(this.results()?.cases??[]).filter(c=>this.showResolved||!c.resolved);}
  selectEvidence(id:string,checked:boolean){this.evidence=checked?[...this.evidence,id]:this.evidence.filter(x=>x!==id);}
  async save(retry=false){const d=this.individual();if(!d||!d.student.sessionId||this.busy())return;const epoch=this.epoch;
    if(!retry){const review=d.reviews.find(r=>(r.exerciseId??'')===this.target);this.pending={requestId:crypto.randomUUID(),revision:review?.revision??0,evidenceRevision:d.evidenceRevision,exerciseId:this.target||null,state:this.state,attention:this.attention,outcome:this.outcome,flagged:this.flagged,comment:this.comment,observation:this.observation,recommendation:this.recommendation,evidenceIds:[...this.evidence]};}
    if(!this.pending)return;this.busy.set(true);this.error.set('');this.saved.set('');try{await firstValueFrom(this.api.review(this.assignmentId,this.studentId,this.pending));if(!this.live(epoch))return;this.reset();await this.load();this.saved.set('Decisión docente guardada. La respuesta y el resultado original permanecen en su historial.');}
    catch(e:unknown){if(this.live(epoch)){this.error.set(this.message(e));const status=(e as {status?:number}).status??0;this.uncertain.set(status===0||status>=500);if(!this.uncertain())this.pending=null;}}
    finally{if(this.live(epoch))this.busy.set(false);}}
  ngOnDestroy(){this.destroyed=true;this.epoch++;this.sub.unsubscribe();}
}

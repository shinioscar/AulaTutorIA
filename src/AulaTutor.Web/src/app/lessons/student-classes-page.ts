import { Component, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AssignedClass, AssignmentService, classState } from './assignment.service';
import { SafeResponse } from '../tutoring/safe-response';

@Component({imports:[RouterLink,DatePipe,SafeResponse],templateUrl:'./student-classes-page.html',styleUrl:'./lessons.css'})
export class StudentClassesPage implements OnDestroy {
  private readonly api=inject(AssignmentService);private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);
  readonly classes=signal<AssignedClass[]>([]);readonly detail=signal<AssignedClass|null>(null);readonly busy=signal(false);readonly error=signal('');readonly state=classState;
  private id='';private destroyed=false;private epoch=0;private keys=new Map<string,string>();
  private readonly sub=this.route.paramMap.subscribe(p=>{this.id=p.get('id')??'';this.detail.set(null);void this.load();});
  async load(){const epoch=++this.epoch;this.busy.set(true);this.error.set('');try{if(this.id){const c=await firstValueFrom(this.api.studentGet(this.id));if(epoch===this.epoch&&!this.destroyed)this.detail.set(c);}else{const rows=await firstValueFrom(this.api.studentList());if(epoch===this.epoch&&!this.destroyed)this.classes.set(rows);}}catch(e:unknown){if(epoch===this.epoch&&!this.destroyed)this.error.set((e as {error?:{title?:string}})?.error?.title??'No se pudieron consultar tus clases. Comprueba tu sesión y relación académica.');}finally{if(epoch===this.epoch&&!this.destroyed)this.busy.set(false);}}
  async open(c:AssignedClass){if(this.busy())return;if(c.sessionId){await this.router.navigate(['/tutoring',c.sessionId]);return;}if(!c.available)return;const epoch=this.epoch;this.busy.set(true);this.error.set('');let key=this.keys.get(c.assignmentId);if(!key){key=crypto.randomUUID();this.keys.set(c.assignmentId,key);}try{const s=await firstValueFrom(this.api.start(c.assignmentId,key));if(!this.destroyed&&epoch===this.epoch)await this.router.navigate(['/tutoring',s.id]);}catch(e:unknown){if(!this.destroyed&&epoch===this.epoch)this.error.set((e as {error?:{title?:string}})?.error?.title??'No se confirmó el inicio. Reintenta: el servidor conserva una única sesión.');}finally{if(!this.destroyed&&epoch===this.epoch)this.busy.set(false);}}
  ngOnDestroy(){this.destroyed=true;this.epoch++;this.sub.unsubscribe();}
}

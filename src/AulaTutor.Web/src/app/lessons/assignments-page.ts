import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom, forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Assignment, AssignmentService, PublishedClass, classState } from './assignment.service';

@Component({imports:[FormsModule,DatePipe,RouterLink],templateUrl:'./assignments-page.html',styleUrl:'./lessons.css'})
export class AssignmentsPage {
  private readonly api=inject(AssignmentService);private readonly destroy=inject(DestroyRef);private readonly route=inject(ActivatedRoute);
  readonly versions=signal<PublishedClass[]>([]);readonly groups=signal<{id:string;name:string}[]>([]);readonly assignments=signal<Assignment[]>([]);
  readonly busy=signal(false);readonly error=signal('');readonly notice=signal('');readonly state=classState;
  versionId=this.route.snapshot.queryParamMap.get('version')??'';groupId='';from='';until='';editing:Assignment|null=null;editFrom='';editUntil='';
  private createKey=crypto.randomUUID();private commands=new Map<string,object>();
  constructor(){void this.load();}
  selected(){return this.versions().find(v=>v.versionId===this.versionId);}
  older(v:PublishedClass){return this.versions().some(other=>other.lessonId===v.lessonId&&other.number>v.number);}
  async load(){this.busy.set(true);this.error.set('');try{const data=await firstValueFrom(forkJoin({versions:this.api.published(),groups:this.api.groups(),assignments:this.api.list()}).pipe(takeUntilDestroyed(this.destroy)));this.versions.set(data.versions);this.groups.set(data.groups);this.assignments.set(data.assignments);}catch(e:unknown){this.error.set(this.reason(e));}finally{this.busy.set(false);}}
  private date(value:string){return value?new Date(value).toISOString():null;}
  private local(value:string|null){if(!value)return '';const d=new Date(value);return new Date(d.getTime()-d.getTimezoneOffset()*60000).toISOString().slice(0,16);}
  reason(e:unknown){return (e as {error?:{title?:string}})?.error?.title??'No se confirmó el cambio. Consulta el estado antes de modificar la solicitud.';}
  edit(a:Assignment){this.editing=a;this.editFrom=this.local(a.availableFrom);this.editUntil=this.local(a.availableUntil);}
  async create(){if(this.busy()||!this.versionId||!this.groupId)return;this.busy.set(true);this.error.set('');this.notice.set('');try{const a=await firstValueFrom(this.api.create({requestId:this.createKey,lessonVersionId:this.versionId,groupId:this.groupId,availableFrom:this.date(this.from),availableUntil:this.date(this.until)}).pipe(takeUntilDestroyed(this.destroy)));this.createKey=crypto.randomUUID();this.assignments.update(rows=>[a,...rows.filter(x=>x.id!==a.id)]);this.notice.set(`Versión ${a.versionNumber} asignada a ${a.groupName}.`);}catch(e){this.error.set(this.reason(e));}finally{this.busy.set(false);}}
  async command(a:Assignment,action:string){if(this.busy())return;if(action==='withdraw'&&!window.confirm('¿Retirar definitivamente esta asignación? Se conservan las evidencias y se detiene el trabajo.'))return;
    const dates=action==='schedule'?{availableFrom:this.date(this.editFrom),availableUntil:this.date(this.editUntil)}:{};
    const signature=JSON.stringify({id:a.id,action,...dates});let request=this.commands.get(signature);if(!request){request={requestId:crypto.randomUUID(),revision:a.revision,...dates};this.commands.set(signature,request);}
    this.busy.set(true);this.error.set('');this.notice.set('');try{const updated=await firstValueFrom(this.api.command(a.id,action,request).pipe(takeUntilDestroyed(this.destroy)));this.assignments.update(rows=>rows.map(x=>x.id===a.id?updated:x));this.commands.delete(signature);if(this.editing?.id===a.id)this.editing=null;this.notice.set('Cambio de asignación guardado.');}catch(e){this.error.set(this.reason(e));}finally{this.busy.set(false);}}
}

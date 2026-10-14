import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { Observable, Subscription } from 'rxjs';
import { LessonContent, LessonExercise, LessonService, LessonView, LessonOperation, LessonVersion, emptyLesson, lessonState, lessonAction } from './lesson.service';
import { SafeResponse } from '../tutoring/safe-response';
import { LessonSummary } from './lesson-summary';
import { aiErrorMessage } from '../ai/ai-error-message';
@Component({selector:'app-lesson-editor',imports:[FormsModule,RouterLink,DatePipe,SafeResponse,LessonSummary],templateUrl:'./lesson-editor.html',styleUrl:'./lessons.css'})
export class LessonEditor {
  private readonly service=inject(LessonService);private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);private readonly destroy=inject(DestroyRef);
  readonly lesson=signal<LessonView|null>(null);readonly busy=signal(false);readonly error=signal('');readonly notice=signal('');readonly dirty=signal(false);readonly tab=signal('content');readonly version=signal<LessonVersion|null>(null);readonly state=lessonState;
  content:LessonContent=emptyLesson();prompt='';designAction='Plan';private id='';private polling=false;private readonly timer:ReturnType<typeof setInterval>;
  readonly generating=signal(false);private designRequest:Subscription|null=null;
  private mutationEpoch=0;
  readonly actionLabel=lessonAction;
  readonly aiError=aiErrorMessage;readonly now=signal(Date.now());
  constructor(){this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(p=>{this.id=p.get('id')??'';this.dirty.set(false);this.version.set(null);this.refresh();});this.timer=setInterval(()=>{this.now.set(Date.now());if((this.generating()||this.lesson()?.operations.some(o=>o.isGenerating))&&!this.polling)this.poll();},2000);this.destroy.onDestroy(()=>clearInterval(this.timer));}
  editable(){return ['Draft','Simulation','InReview'].includes(this.lesson()?.state??'');}
  changed(){this.dirty.set(true);this.notice.set('Cambios sin guardar');}
  private accept(l:LessonView,replace=false){if(this.lesson()?.id===l.id&&(this.lesson()?.revision??-1)>l.revision)return;this.lesson.set(l);if(replace||!this.dirty()){this.content=structuredClone(l.content);this.dirty.set(false);}}
  private run(obs:Observable<LessonView>,notice:string,replace=false,mutation=true){if(this.busy())return;const epoch=mutation?++this.mutationEpoch:this.mutationEpoch;this.busy.set(true);this.error.set('');obs.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{if(mutation){this.mutationEpoch++;this.accept(l,replace);}else if(epoch===this.mutationEpoch)this.accept(l,replace);this.busy.set(false);this.notice.set(notice);},error:e=>{if(mutation)this.mutationEpoch++;this.busy.set(false);this.error.set(e.error?.title??'No se completó la operación. Conservamos tus cambios; consulta el estado guardado.');}});}
  refresh(){this.run(this.service.get(this.id),'Estado guardado actualizado.',false,false);}
  discardLocal(){this.dirty.set(false);this.refresh();}
  private poll(){const epoch=this.mutationEpoch;this.polling=true;this.service.get(this.id).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{if(epoch===this.mutationEpoch)this.accept(l);this.polling=false;},error:()=>{this.polling=false;}});}
  save(){const l=this.lesson();if(l)this.run(this.service.save(this.id,l.revision,this.content),'Borrador guardado.',true);}
  command(action:string,targetId:string|null=null){const l=this.lesson();if(!l||this.dirty())return;this.run(this.service.command(this.id,action,l.revision,targetId),'Operación guardada.',true);}
  private runDesign(obs:Observable<LessonView>){if(this.generating())return;this.mutationEpoch++;this.generating.set(true);this.error.set('');this.notice.set('Generando propuesta… Puedes seguir editando el borrador.');this.designRequest=obs.pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{this.mutationEpoch++;this.accept(l);this.generating.set(false);this.notice.set('Solicitud guardada; revisa su resultado antes de aplicar.');},error:e=>{this.mutationEpoch++;this.generating.set(false);this.notice.set('Solicitud no completada; consulta el estado guardado.');this.error.set(e.error?.title??'No se completó la propuesta. Consulta su estado; el borrador está conservado.');}});this.poll();}
  design(){const l=this.lesson();if(!l||this.dirty()||!this.prompt.trim())return;this.runDesign(this.service.design(this.id,crypto.randomUUID(),l.revision,this.designAction,this.prompt));}
  retryReady(o:LessonOperation){return o.attempts<3&&(!o.retryNotBefore||Date.parse(o.retryNotBefore)<=this.now());}
  retry(o:LessonOperation){const l=this.lesson();if(l&&this.retryReady(o))this.runDesign(this.service.design(this.id,o.requestId,l.revision,o.action,o.text,true));}
  cancel(o:LessonOperation){this.designRequest?.unsubscribe();this.generating.set(false);this.run(this.service.cancel(this.id,o.requestId),'Solicitud cancelada.');}
  async simulate(){const l=this.lesson();if(!l||this.busy()||this.dirty())return;this.busy.set(true);this.error.set('');this.service.start(this.id,l.revision,crypto.randomUUID()).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>{void this.router.navigate(['/teacher/lessons',this.id,'simulations',s.id]);},error:e=>{this.error.set(e.error?.title??'No se pudo iniciar la simulación.');this.busy.set(false);}});}
  addObjective(){this.content.objectives.push({id:'o-'+crypto.randomUUID().slice(0,8),text:''});this.changed();}
  removeObjective(id:string){if(this.content.sections.some(s=>s.exercises.some(e=>e.objectiveId===id))){this.error.set('Reasigna o elimina sus actividades antes de quitar el objetivo.');return;}this.content.objectives=this.content.objectives.filter(o=>o.id!==id);this.changed();}
  addSection(){this.content.sections.push({id:'s-'+crypto.randomUUID().slice(0,8),title:'',content:'',exercises:[]});this.changed();}
  move(index:number,delta:number){const target=index+delta;if(target<0||target>=this.content.sections.length)return;[this.content.sections[index],this.content.sections[target]]=[this.content.sections[target]!,this.content.sections[index]!];this.changed();}
  removeSection(index:number){this.content.sections.splice(index,1);this.changed();}
  addExercise(index:number){const objective=this.content.objectives[0];if(!objective){this.error.set('Añade un objetivo antes de crear actividades.');return;}this.content.sections[index]?.exercises.push({id:'e-'+crypto.randomUUID().slice(0,8),objectiveId:objective.id,required:true,activity:{kind:'OpenResponse',prompt:'',options:[],acceptedAnswers:[],rubric:''},rubric:{criteria:[],referenceSolution:'',hintRules:''}});this.changed();}
  changeKind(e:LessonExercise){e.activity.options=e.activity.kind==='TrueFalse'?['Verdadero','Falso']:e.activity.kind==='MultipleChoice'?['Opción A','Opción B']:[];e.activity.acceptedAnswers=e.activity.kind==='TrueFalse'?['Falso']:e.activity.kind==='MultipleChoice'?['Opción A']:e.activity.kind==='ShortAnswer'?['Respuesta']:[];this.changed();}
  lines(value:string){return value.split('\n').map(x=>x.trim()).filter(Boolean);}
  setOptions(e:LessonExercise,value:string){e.activity.options=this.lines(value);this.changed();}
  setAnswers(e:LessonExercise,value:string){e.activity.acceptedAnswers=this.lines(value);this.changed();}
  setCriteria(e:LessonExercise,value:string){e.rubric.criteria=this.lines(value);this.changed();}
  objectiveText(id:string){return this.content.objectives.find(o=>o.id===id)?.text??'';}
}

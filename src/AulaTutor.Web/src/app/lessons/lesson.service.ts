import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
export interface ExerciseActivity { prompt:string; kind:string; options:string[]; acceptedAnswers:string[]; rubric:string; }
export interface LessonExercise { id:string; objectiveId:string; required:boolean; activity:ExerciseActivity; rubric:{criteria:string[];referenceSolution:string;hintRules:string}; }
export interface PreloadedExercise {exerciseId:string;hints:string[];alternatives:string[];correctFeedback:string;incorrectFeedback:string;reviewFeedback:string;commonErrors:string[];reinforcementSuggestions:string[];}
export interface LessonContent {title:string;subject:string;description:string;prerequisites:string;level:string;durationMinutes:number;methodology:string;instructions:string;objectives:{id:string;text:string}[];sections:{id:string;title:string;content:string;exercises:LessonExercise[]}[];preloaded?:{introduction:string;exercises:PreloadedExercise[]}|null;}
export interface LessonOperation {requestId:string;simulationId:string|null;action:string;text:string;draftRevision:number;state:string;isGenerating:boolean;errorCode:string|null;attempts:number;retryNotBefore:string|null;reply:string|null;outcome:string|null;validationType:string|null;providerId:string;modelId:string;}
export interface LessonVersion {id:string;number:number;draftRevision:number;content:LessonContent;contentHash:string;approvedBy:string;approvedAt:string;publishedAt:string|null;archivedAt:string|null;}
export interface LessonView {id:string;state:string;revision:number;draftRevision:number;content:LessonContent;validation:{errors:string[];warnings:string[]};proposals:{id:string;draftRevision:number;state:string;explanation:string;content:LessonContent;outdated:boolean}[];versions:LessonVersion[];operations:LessonOperation[];simulations:{id:string;draftRevision:number;state:string;revision:number}[];}
export interface LessonItem {id:string;title:string;state:string;revision:number;updatedAt:string;}
export interface LessonExternalOptions {topic:string;subject:string;level:string;goals:string;exerciseCount:number;difficulty:string;activityTypes:string;hintCount:number;durationMinutes:number;notes:string;rules:string;preloaded?:boolean;}
export interface LessonImportPreview {content:LessonContent|null;errors:string[];warnings:string[];correctionInstructions:string;}
export interface SimulationView {id:string;lessonId:string;draftRevision:number;state:string;revision:number;title:string;sectionTitle:string;sectionContent:string;exercise:{id:string;kind:string;prompt:string;options:string[]}|null;sectionIndex:number;sectionCount:number;hints:number;answered:boolean;operations:LessonOperation[];preloaded:boolean;}
export const emptyLesson = ():LessonContent => ({title:'',subject:'',description:'',prerequisites:'',level:'',durationMinutes:0,methodology:'',instructions:'',objectives:[],sections:[]});
export const lessonState = (state:string):string => ({Draft:'Borrador',Simulation:'En simulación',InReview:'En revisión',Approved:'Aprobada',Published:'Publicada',Archived:'Archivada',Active:'Activa',Stopped:'Detenida',Pending:'Pendiente',Succeeded:'Completada',Failed:'Fallida',Cancelled:'Cancelada',Applied:'Aplicada',Discarded:'Descartada'}[state]??state);
export const outcomeLabel = (outcome:string):string => ({CorrectWithoutHelp:'Coincide con la regla, sin ayuda',CorrectWithHelp:'Coincide con la regla, con ayuda',Incorrect:'No coincide con la regla',NeedsReview:'Requiere revisión humana',NotEvaluable:'Sin regla verificable'}[outcome]??outcome);
export const exerciseKind = (kind:string):string => ({MultipleChoice:'Opción múltiple',TrueFalse:'Verdadero/falso',ShortAnswer:'Respuesta corta',OpenResponse:'Respuesta abierta',Code:'Código o SQL sin ejecución'}[kind]??kind);
export const lessonAction = (action:string):string => ({Start:'Inicio',Next:'Avance',Answer:'Respuesta',Message:'Duda',Hint:'Pista',Simplify:'Simplificación',PreloadedPlan:'Clase precargada completa',Plan:'Plan completo',Content:'Contenido',Exercises:'Ejercicios',Rubrics:'Rúbricas'}[action]??action);
@Injectable({providedIn:'root'})
export class LessonService {
  private readonly http=inject(HttpClient);
  externalPrompt(options:LessonExternalOptions){return this.http.post<{prompt:string}>('/api/lessons/external-prompt',options);}
  previewImport(json:string){return this.http.post<LessonImportPreview>('/api/lessons/import/preview',{json});}
  importLesson(json:string,requestId:string){return this.http.post<LessonView>('/api/lessons/import',{json,requestId});}
  list(page=1){return this.http.get<LessonItem[]>('/api/lessons/?page='+page);}
  create(content:LessonContent,requestId:string){return this.http.post<LessonView>('/api/lessons/',{requestId,content});}
  get(id:string){return this.http.get<LessonView>('/api/lessons/'+id);}
  save(id:string,revision:number,content:LessonContent){return this.http.put<LessonView>('/api/lessons/'+id+'/draft',{revision,content});}
  command(id:string,action:string,revision:number,targetId:string|null=null,requestId=crypto.randomUUID()){return this.http.post<LessonView>(`/api/lessons/${id}/${action}`,{requestId,revision,targetId});}
  design(id:string,requestId:string,revision:number,action:string,text:string,retry=false){return this.http.post<LessonView>(`/api/lessons/${id}/design`,{requestId,revision,action,text,retry});}
  cancel(id:string,requestId:string){return this.http.post<LessonView>(`/api/lessons/${id}/design/${requestId}/cancel`,{});}
  start(id:string,revision:number,requestId:string){return this.http.post<SimulationView>(`/api/lessons/${id}/simulations`,{revision,requestId});}
  simulation(id:string,simId:string){return this.http.get<SimulationView>(`/api/lessons/${id}/simulations/${simId}`);}
  turn(id:string,simId:string,requestId:string,revision:number,action:string,text:string|null=null,retry=false){return this.http.post<SimulationView>(`/api/lessons/${id}/simulations/${simId}/messages`,{requestId,revision,action,text,retry});}
  simulationCommand(id:string,simId:string,action:string,revision:number,targetId:string|null=null){return this.http.post<SimulationView>(`/api/lessons/${id}/simulations/${simId}/${action}`,{requestId:crypto.randomUUID(),revision,targetId});}
}

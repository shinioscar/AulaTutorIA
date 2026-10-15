import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Session } from '../tutoring/tutoring.service';

export interface PublishedClass {lessonId:string;versionId:string;number:number;title:string;subject:string;publishedAt:string;contentHash:string;}
export interface Assignment {id:string;lessonId:string;lessonVersionId:string;versionNumber:number;title:string;subject:string;contentHash:string;groupId:string;groupName:string;teacherName:string;state:string;revision:number;availableFrom:string|null;availableUntil:string|null;available:boolean;changes:{action:string;state:string;changedAt:string;availableFrom:string|null;availableUntil:string|null}[];}
export interface AssignedClass {assignmentId:string;lessonVersionId:string;versionNumber:number;contentHash:string;title:string;subject:string;teacherName:string;groupName:string;state:string;availableFrom:string|null;availableUntil:string|null;available:boolean;sessionId:string|null;studentState:string;deliveredRequired:number;totalRequired:number;verifiedRequired:number;methodology:string;instructions:string;prerequisites:string;objectives:{id:string;text:string;delivered:number;required:number}[];sections:{id:string;title:string;content:string}[];}
export interface AssignedSession {class:AssignedClass;currentExerciseId:string|null;sectionTitle:string;sectionContent:string;canComplete:boolean;unavailableReason:string|null;}
export const classState=(value:string)=>({Available:'Disponible',InProgress:'En progreso',Paused:'Pausada',Completed:'Completada · entrega registrada',Unavailable:'No disponible',Active:'Activa',Inactive:'Desactivada',Withdrawn:'Retirada'} as Record<string,string>)[value]??value;
@Injectable({providedIn:'root'})
export class AssignmentService {
  private readonly http=inject(HttpClient);
  published(){return this.http.get<PublishedClass[]>('/api/teacher/assignments/published-versions');}
  groups(){return this.http.get<{id:string;name:string}[]>('/api/teacher/assignments/authorized-groups');}
  list(){return this.http.get<Assignment[]>('/api/teacher/assignments/');}
  create(request:object){return this.http.post<Assignment>('/api/teacher/assignments/',request);}
  command(id:string,action:string,request:object){return this.http.post<Assignment>(`/api/teacher/assignments/${id}/${action}`,request);}
  studentList(){return this.http.get<AssignedClass[]>('/api/students/me/classes/');}
  studentGet(id:string){return this.http.get<AssignedClass>(`/api/students/me/classes/${id}`);}
  start(id:string,requestId:string){return this.http.post<Session>(`/api/students/me/classes/${id}/start`,{requestId});}
}

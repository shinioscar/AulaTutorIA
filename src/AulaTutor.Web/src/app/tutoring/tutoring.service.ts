import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface SessionItem { id:string; topic:string; goal:string; state:string; providerId:string; modelId:string; lastActivityAt:string; }
export interface Message { id:string; requestId:string; sequence:number; role:'Student'|'Tutor'; text:string; createdAt:string; providerId:string|null; modelId:string|null; }
export interface Turn { requestId:string; action:string; state:string; attempts:number; isGenerating:boolean; canRetry:boolean; error:string|null; retryNotBefore:string|null; }
export interface Session extends SessionItem { level:string; directReview:boolean; revision:number; messages:Message[]; turns:Turn[]; pendingRequestId:string|null; summary:string|null; activity:string; currentFocus:string; }
export interface TurnRequest { requestId:string; revision:number; action:string; text:string|null; retry:boolean; }
@Injectable({providedIn:'root'})
export class TutoringService {
  private readonly http = inject(HttpClient);
  list(page:number) { return this.http.get<SessionItem[]>('/api/students/me/tutoring-sessions',{params:{page}}); }
  create(request:object) { return this.http.post<Session>('/api/tutoring-sessions/',request); }
  get(id:string) { return this.http.get<Session>(`/api/tutoring-sessions/${id}`); }
  send(id:string,request:TurnRequest) { return this.http.post<Session>(`/api/tutoring-sessions/${id}/messages`,request); }
  transition(id:string,action:string,revision:number) { return this.http.post<Session>(`/api/tutoring-sessions/${id}/${action}`,{revision}); }
  cancel(id:string,requestId:string,revision:number) { return this.http.post<Session>(`/api/tutoring-sessions/${id}/turns/${requestId}/cancel`,{revision}); }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import type { AssignedSession } from '../lessons/assignment.service';

export interface SessionItem { id:string; topic:string; goal:string; state:string; providerId:string; modelId:string; lastActivityAt:string; }
export interface Message { id:string; requestId:string; sequence:number; role:'Student'|'Tutor'; text:string; createdAt:string; providerId:string|null; modelId:string|null; }
export interface Turn { requestId:string; action:string; state:string; attempts:number; isGenerating:boolean; canRetry:boolean; error:string|null; retryNotBefore:string|null; exerciseId:string|null; retryText:string|null; }
export interface Observation {text:string; evidenceIds:string[];}
export interface Exercise {id:string; version:number; isDiagnostic:boolean; kind:string; prompt:string; options:string[]; rubric:string; hints:number; completed:boolean; order:number; objectiveOrder:number;simplifications:number;lessonVersionId:string|null;approvedExerciseId:string|null;approvedObjectiveId:string|null;required:boolean;isComplementary:boolean;}
export interface Attempt {id:string; requestId:string; exerciseId:string; answer:string; number:number; hintsUsed:number; outcome:string; validationType:string; feedback:string;simplificationsUsed:number;}
export interface Diagnostic {id:string; version:number; preliminary:boolean; outdated:boolean; content:{summary:string;strengths:Observation[];difficulties:Observation[];notAssessed:string[];practice:string[]};}
export interface Plan {id:string;version:number;state:string;currentActivity:number;content:{mainGoal:string;progression:string;objectives:{title:string;progress:string;evidenceIds:string[];activities:{explanation:string;difficulty:string;estimatedMinutes:number}[]}[]};}
export interface Report {id:string;version:number;outdated:boolean;providerId:string;modelId:string;promptVersion:string;topic:string;goal:string;statistics:{exercises:number;attempts:number;correctWithoutHelp:number;correctWithHelp:number;incorrect:number;needsReview:number;notEvaluable:number;hints:number;workedObjectives:string[]};content:{summary:string;observations:Observation[];notAssessed:string[];recommendations:{goal:string;evidenceIds:string[]}[]};}
export interface Share {id:string;reportId:string;recipientId:string;recipientName:string;revokedAt:string|null;}
export interface Learning {evidenceRevision:number;diagnosticQuestionLimit:number;minimumEvidence:number;exercises:Exercise[];attempts:Attempt[];diagnostic:Diagnostic|null;plan:Plan|null;reports:Report[];recommendations:{id:string;reportId:string;goal:string;isGeneral:boolean}[];shares:Share[];}
export interface Session extends SessionItem { level:string; directReview:boolean; revision:number; messages:Message[]; turns:Turn[]; pendingRequestId:string|null; summary:string|null; activity:string; currentFocus:string; learning:Learning;mode:string;assignedClass:AssignedSession|null;policyVersion:string; }
export interface TurnRequest { requestId:string; revision:number; action:string; text:string|null; retry:boolean; exerciseId?:string|null; }
export interface LearningAction {action:string;text?:string;exerciseId?:string;}
@Injectable({providedIn:'root'})
export class TutoringService {
  private readonly http = inject(HttpClient);
  list(page:number) { return this.http.get<SessionItem[]>('/api/students/me/tutoring-sessions',{params:{page}}); }
  create(request:object) { return this.http.post<Session>('/api/tutoring-sessions/',request); }
  get(id:string) { return this.http.get<Session>(`/api/tutoring-sessions/${id}`); }
  card(id:string,request:TurnRequest){return this.http.post<Session>(`/api/tutoring-sessions/${id}/cards`,request);}
  send(id:string,request:TurnRequest) { return this.http.post<Session>(`/api/tutoring-sessions/${id}/messages`,request); }
  transition(id:string,action:string,revision:number) { return this.http.post<Session>(`/api/tutoring-sessions/${id}/${action}`,{revision}); }
  cancel(id:string,requestId:string,revision:number) { return this.http.post<Session>(`/api/tutoring-sessions/${id}/turns/${requestId}/cancel`,{revision}); }
  recipients(id:string) {return this.http.get<{id:string;displayName:string}[]>(`/api/tutoring-sessions/${id}/share-recipients`);}
  share(id:string,reportId:string,recipientId:string,requestId:string) {return this.http.post<Share>(`/api/tutoring-sessions/${id}/shares`,{requestId,reportId,recipientId,consent:true});}
  revoke(id:string,shareId:string) {return this.http.delete<void>(`/api/tutoring-sessions/${id}/shares/${shareId}`);}
  reinforce(id:string,recommendationId:string,requestId:string) {return this.http.post<Session>(`/api/tutoring-sessions/${id}/recommendations/${recommendationId}/start`,{requestId});}
  sharedReports() {return this.http.get<Report[]>('/api/teacher/reports/');}
}

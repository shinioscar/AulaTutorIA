import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { SafeResponse } from '../tutoring/safe-response';
import { TeacherReview, supervisionLabel } from './supervision.service';

@Component({selector:'app-review-history',imports:[DatePipe,SafeResponse],template:`
@for(r of reviews();track r.id){<article><h3>{{r.target}}</h3>
  @for(h of r.history;track h.id){<details [open]="$first"><summary>Decisión docente {{h.revision}} · {{h.authorName}} · {{h.createdAt|date:'short'}} · {{label(h.state)}}</summary>
    <p>{{label(h.attention)}} · {{label(h.outcome)}}</p>
    <app-safe-response [text]="h.comment"/>
    @if(h.observation){<h4>Observación pedagógica</h4><app-safe-response [text]="h.observation"/>}
    @if(h.recommendation){<h4>Recomendación de refuerzo</h4><app-safe-response [text]="h.recommendation"/>}
    <p class="note">Revisión de evidencia {{h.evidenceRevision}}. Intentos examinados: {{h.evidenceIds.length}}.</p>
    @if(h.evidenceIds.length){<details><summary>Referencias de evidencia</summary><ul>@for(id of h.evidenceIds;track id){<li>{{id}}</li>}</ul></details>}
  </details>}
</article>}@empty{<p>Aún no hay observaciones docentes para esta clase.</p>}
`,styleUrl:'../lessons/lessons.css'})
export class ReviewHistory {readonly reviews=input.required<TeacherReview[]>();readonly label=supervisionLabel;}

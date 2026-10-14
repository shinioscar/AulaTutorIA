import { Component, input } from '@angular/core';
import { LessonContent, exerciseKind } from './lesson.service';
import { SafeResponse } from '../tutoring/safe-response';
@Component({selector:'app-lesson-summary',imports:[SafeResponse],styles:[':host{display:block;min-width:0;overflow-wrap:anywhere}'],template:`
  <h3>{{content().title || 'Sin título'}}</h3><p>{{content().subject}} · {{content().level}} · {{content().durationMinutes}} minutos orientativos</p>
  <p>{{content().description}}</p><p><strong>Requisitos previos:</strong> {{content().prerequisites || 'No indicados'}}</p>
  <h4>Objetivos</h4><ol>@for(o of content().objectives;track o.id){<li>{{o.text}}</li>}</ol>
  <p><strong>Metodología:</strong> {{content().methodology}}</p><p><strong>Instrucciones:</strong> {{content().instructions}}</p>
  @for(s of content().sections;track s.id){<article><h4>{{s.title}}</h4><app-safe-response [text]="s.content"/>
    @for(e of s.exercises;track e.id){<p><strong>{{e.required?'Actividad obligatoria':'Actividad opcional'}} ({{kind(e.activity.kind)}})</strong> · Objetivo {{e.objectiveId}}</p><app-safe-response [text]="e.activity.prompt"/>
      @if(e.activity.options.length){<ul>@for(option of e.activity.options;track $index){<li>{{option}}</li>}</ul>}
      <p>Criterios docentes: {{e.rubric.criteria.join('; ')}}</p><p>Rúbrica: {{e.activity.rubric}}</p><p>Reglas de pistas: {{e.rubric.hintRules}}</p>
      <details><summary>Solución y reglas privadas · solo docente propietario</summary><p>Alternativas aceptadas: {{e.activity.acceptedAnswers.join('; ')}}</p><app-safe-response [text]="e.rubric.referenceSolution"/></details>
    }
  </article>}
`})
export class LessonSummary {readonly content=input.required<LessonContent>();readonly kind=exerciseKind;}

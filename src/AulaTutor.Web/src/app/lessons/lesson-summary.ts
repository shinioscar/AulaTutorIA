import { Component, input } from '@angular/core';
import { LessonContent, exerciseKind } from './lesson.service';
import { SafeResponse } from '../tutoring/safe-response';
@Component({selector:'app-lesson-summary',imports:[SafeResponse],styles:[':host{display:block;min-width:0;overflow-wrap:anywhere}'],template:`
  <h3>{{content().title || 'Sin título'}}</h3><p>{{content().subject}} · {{content().level}} · {{content().durationMinutes}} minutos orientativos</p>
  <p><strong>Modalidad: {{content().preloaded?'Precargada, con tarjetas y ayudas locales':'Conversacional'}}</strong></p>
  @if(!content().preloaded){<p class="note">Para usar tarjetas sin IA durante los ejercicios, completa el contenido precargado en el constructor. Una importación V1 conserva la modalidad conversacional.</p>}
  <p>{{content().description}}</p><p><strong>Requisitos previos:</strong> {{content().prerequisites || 'No indicados'}}</p>
  @if(content().preloaded;as p){<p><strong>Modalidad precargada · tarjetas y ayudas locales.</strong></p><app-safe-response [text]="p.introduction"/><details><summary>Revisar todas las ayudas precargadas · solo docente propietario</summary>@for(a of p.exercises;track a.exerciseId){<h4>Actividad {{a.exerciseId}}</h4><h5>Pistas progresivas</h5><ol>@for(h of a.hints;track $index){<li>{{h}}</li>}</ol><h5>Explicaciones alternativas</h5><ul>@for(x of a.alternatives;track $index){<li>{{x}}</li>}</ul><p>Feedback de coincidencia: {{a.correctFeedback}}</p><p>Feedback de error: {{a.incorrectFeedback}}</p><p>Feedback de revisión humana: {{a.reviewFeedback}}</p><p>Errores comunes: {{a.commonErrors.join('; ')}}</p><p>Refuerzos sugeridos: {{a.reinforcementSuggestions.join('; ')}}</p>}</details>}
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

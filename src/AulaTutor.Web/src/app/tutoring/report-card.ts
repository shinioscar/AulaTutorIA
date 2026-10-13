import { Component, input } from '@angular/core';
import { Report } from './tutoring.service';
import { SafeResponse } from './safe-response';
@Component({selector:'app-report-card',standalone:true,imports:[SafeResponse],template:`
  @if(report();as r){
    <h3>Reporte orientativo · revisión {{r.version}}</h3>
    <p><strong>{{r.topic}}</strong> · {{r.goal}}</p>
    <p class="note">{{r.providerId}} · {{r.modelId}} · {{r.promptVersion}}. Interpretación IA sujeta a revisión, sin nota oficial.</p>
    @if(r.outdated){<p class="notice">Desactualizado: hay cambios en las evidencias después de esta revisión.</p>}
    <app-safe-response [text]="r.content.summary" />
    <dl class="statistics"><div><dt>Ejercicios presentados</dt><dd>{{r.statistics.exercises}}</dd></div><div><dt>Intentos registrados</dt><dd>{{r.statistics.attempts}}</dd></div><div><dt>Correctos sin ayuda</dt><dd>{{r.statistics.correctWithoutHelp}}</dd></div><div><dt>Correctos con ayuda</dt><dd>{{r.statistics.correctWithHelp}}</dd></div><div><dt>Intentos incorrectos</dt><dd>{{r.statistics.incorrect}}</dd></div><div><dt>Pendientes de revisión</dt><dd>{{r.statistics.needsReview}}</dd></div><div><dt>No evaluables</dt><dd>{{r.statistics.notEvaluable}}</dd></div><div><dt>Pistas recibidas</dt><dd>{{r.statistics.hints}}</dd></div></dl>
    <p class="note">Estadísticas calculadas por la aplicación desde PostgreSQL. No representan porcentajes de dominio.</p>
    <h4>Objetivos trabajados en la ruta</h4><ul>@for(item of r.statistics.workedObjectives;track $index){<li>{{item}}</li>}@empty{<li>No hay objetivos de plan con intentos registrados.</li>}</ul>
    @for(o of r.content.observations;track $index){<p>{{o.text}}</p><small class="evidence-reference">Evidencias: {{o.evidenceIds.join(', ')}}</small>}
    <h4>Aspectos no evaluados</h4><ul>@for(item of r.content.notAssessed;track $index){<li>{{item}}</li>}</ul>
    <h4>Prácticas siguientes</h4><ul>@for(item of r.content.recommendations;track $index){<li>{{item.goal}}</li>}</ul>
  }
`,styles:`.statistics{display:grid;grid-template-columns:repeat(auto-fit,minmax(135px,1fr));gap:var(--space-3)}.statistics div{background:var(--surface-secondary);border-radius:var(--radius-medium);padding:var(--space-3)}.statistics dt{font-size:var(--font-small)}.statistics dd{font-size:var(--font-heading);margin:var(--space-1) 0 0;font-weight:var(--weight-semibold)}.evidence-reference{display:block;overflow-wrap:anywhere;color:var(--text-secondary)}`})
export class ReportCard {readonly report=input.required<Report>();}

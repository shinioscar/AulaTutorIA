import { Component, computed, input } from '@angular/core';
import { ClassSupervision } from './supervision.service';

@Component({selector:'app-supervision-charts',template:`
<div class="chart-grid">
  <figure aria-labelledby="participation-title">
    <figcaption id="participation-title">Estado de las sesiones</figcaption>
    <p class="note">Estudiantes elegibles de esta asignación · {{total()}} en total</p>
    @if(total()){
      <div class="distribution">
        <svg viewBox="0 0 120 120" aria-hidden="true" focusable="false">
          <circle class="ring-track" cx="60" cy="60" r="46" fill="none" stroke-width="14"/>
          @for(s of segments();track s.label){<circle [class]="'ring '+s.tone" cx="60" cy="60" r="46" fill="none" stroke-width="14" pathLength="100" [attr.stroke-dasharray]="s.percent+' '+(100-s.percent)" [attr.stroke-dashoffset]="-s.offset" transform="rotate(-90 60 60)"/>}
          <text x="60" y="59" text-anchor="middle" class="ring-total">{{total()}}</text>
          <text x="60" y="73" text-anchor="middle" class="ring-label">elegibles</text>
        </svg>
        <table><caption class="visually-hidden">Estudiantes por estado de sesión</caption><thead><tr><th>Estado</th><th>Estudiantes</th></tr></thead><tbody>
          @for(s of segments();track s.label){<tr><th scope="row"><span [class]="'legend '+s.tone" aria-hidden="true"></span>{{s.label}}</th><td>{{s.count}}</td></tr>}
        </tbody></table>
      </div>
    } @else {<p class="chart-empty">Sin estudiantes elegibles para representar.</p>}
  </figure>
  <figure aria-labelledby="objectives-title">
    <figcaption id="objectives-title">Entregas por objetivo</figcaption>
    <p class="note">Pares estudiante / actividad obligatoria que permiten avanzar.</p>
    @for(o of data().objectives;track o.id){
      <div class="objective-row"><span>{{o.text}}</span><strong>{{o.deliveredRequired}} / {{o.eligibleRequiredSlots}}</strong>
        <progress [value]="o.deliveredRequired" [max]="o.eligibleRequiredSlots || 1" [attr.aria-label]="o.text+': '+o.deliveredRequired+' de '+o.eligibleRequiredSlots+' entregas obligatorias' "></progress>
        @if(!o.eligibleRequiredSlots){<small class="note">Sin oportunidades obligatorias en esta cohorte.</small>}
      </div>
    } @empty {<p class="chart-empty">Sin objetivos para representar.</p>}
    <p class="note">Incluye abiertos entregados que requieren revisión humana. El avance no es una calificación.</p>
  </figure>
</div>`,styleUrl:'./supervision-charts.css'})
export class SupervisionCharts {
  readonly data=input.required<ClassSupervision>();
  readonly total=computed(()=>this.data().assignment.counts.eligibleStudents);
  readonly segments=computed(()=>{
    const c=this.data().assignment.counts;
    const rows=[{label:'Finalizadas',count:c.completed,tone:'completed'},
      {label:'En curso',count:c.active,tone:'active'},
      {label:'Pausadas',count:c.paused,tone:'paused'},
      {label:'Abiertas sin comenzar',count:c.opened-c.active-c.paused-c.completed,tone:'created'},
      {label:'Sin abrir',count:c.notOpened,tone:'unopened'}];
    let offset=0;
    return rows.map(row=>{const percent=this.total()?row.count/this.total()*100:0;const segment={...row,percent,offset};offset+=percent;return segment;});
  });
}

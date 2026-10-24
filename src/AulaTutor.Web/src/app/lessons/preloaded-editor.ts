import { Component,input,output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LessonContent,PreloadedExercise } from './lesson.service';

@Component({selector:'app-preloaded-editor',imports:[FormsModule],template:`
<details><summary>Contenido precargado y modalidad</summary>
<p>{{content().preloaded?'Tarjetas con actividades y ayudas preparadas.':'Clase conversacional con apoyo de IA.'}}</p>
@if(!content().preloaded){<button type="button" class="secondary" (click)="enable()">Preparar modalidad precargada</button>}
@else{<button type="button" class="secondary" (click)="content().preloaded=null;changed.emit()">Volver a modalidad conversacional en este borrador</button>
@if(content().preloaded;as p){<label>Introducción (hasta 600 caracteres)<textarea name="preloaded-intro" [(ngModel)]="p.introduction" maxlength="600" (ngModelChange)="changed.emit()"></textarea></label>
<p>Prepara pistas graduales y revisa las ayudas antes de publicar.</p>
@for(e of activities();track e.id){@if(aid(e.id);as a){<fieldset><legend>{{e.activity.prompt||e.id}}</legend>
<label>Pistas progresivas (una por línea, 1–3, hasta 600 caracteres cada una)<textarea [name]="e.id+'-pre-hints'" [ngModel]="a.hints.join('\n')" (ngModelChange)="lines(a,'hints',$event)"></textarea></label>
<label>Explicaciones alternativas (una por línea, 1–3)<textarea [name]="e.id+'-pre-alts'" [ngModel]="a.alternatives.join('\n')" (ngModelChange)="lines(a,'alternatives',$event)"></textarea></label>
<label>Feedback cuando coincide con la regla<textarea [name]="e.id+'-pre-correct'" [(ngModel)]="a.correctFeedback" maxlength="600" (ngModelChange)="changed.emit()"></textarea></label>
<label>Feedback ante error<textarea [name]="e.id+'-pre-incorrect'" [(ngModel)]="a.incorrectFeedback" maxlength="600" (ngModelChange)="changed.emit()"></textarea></label>
<label>Feedback pendiente de revisión humana<textarea [name]="e.id+'-pre-review'" [(ngModel)]="a.reviewFeedback" maxlength="600" (ngModelChange)="changed.emit()"></textarea></label>
<label>Consejos sobre errores comunes (uno por línea, hasta 3)<textarea [name]="e.id+'-pre-errors'" [ngModel]="a.commonErrors.join('\n')" (ngModelChange)="lines(a,'commonErrors',$event)"></textarea></label>
<label>Sugerencias de refuerzo (una por línea, hasta 3)<textarea [name]="e.id+'-pre-reforcements'" [ngModel]="a.reinforcementSuggestions.join('\n')" (ngModelChange)="lines(a,'reinforcementSuggestions',$event)"></textarea></label>
</fieldset>}}
<button type="button" class="secondary" (click)="sync()">Sincronizar ayudas tras añadir o eliminar actividades</button>
}}
</details>`})
export class PreloadedEditor {
 readonly content=input.required<LessonContent>();readonly changed=output<void>();
 activities(){return this.content().sections.flatMap(s=>s.exercises);}
 private empty(id:string):PreloadedExercise{return {exerciseId:id,hints:[],alternatives:[],correctFeedback:'',incorrectFeedback:'',reviewFeedback:'',commonErrors:[],reinforcementSuggestions:[]};}
 enable(){this.content().preloaded={introduction:'',exercises:this.activities().map(e=>this.empty(e.id))};this.changed.emit();}
 aid(id:string){return this.content().preloaded?.exercises.find(a=>a.exerciseId===id);}
 sync(){const p=this.content().preloaded;if(p)p.exercises=this.activities().map(e=>this.aid(e.id)??this.empty(e.id));this.changed.emit();}
 lines(a:PreloadedExercise,key:'hints'|'alternatives'|'commonErrors'|'reinforcementSuggestions',value:string){a[key]=value.split('\n').map(s=>s.trim()).filter(Boolean);this.changed.emit();}
}

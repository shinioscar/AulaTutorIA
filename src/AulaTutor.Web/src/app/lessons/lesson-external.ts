import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LessonImportPreview, LessonService } from './lesson.service';
import { LessonSummary } from './lesson-summary';

@Component({selector:'app-lesson-external',imports:[FormsModule,LessonSummary],styleUrl:'./lessons.css',template:`
<section class="creation-form" aria-labelledby="external-title"><h2 id="external-title">Crear con ChatGPT</h2>
<p>No requiere una API key. También puedes usar otra herramienta compatible. No incluyas datos privados.</p>
<button type="button" class="secondary" (click)="back.emit()" [disabled]="busy()">Regresar a editar opciones</button>
<h3>1. Copiar prompt</h3><label for="external-prompt">Instrucciones para la herramienta externa</label>
<textarea id="external-prompt" [value]="prompt()" readonly rows="8"></textarea>
<div class="actions"><button type="button" (click)="copy(prompt())">Copiar prompt</button>
<a href="https://chatgpt.com/" target="_blank" rel="noopener noreferrer" class="button-link">Abrir ChatGPT ↗</a></div>
<p class="note">Solo se abre su sitio oficial; AulaTutor no envía el prompt ni accede a tu cuenta.</p>
<h3>2. Importar clase</h3><p>Copia la clase generada por ChatGPT y pégala aquí. AulaTutor organizará las actividades automáticamente.</p>
<label for="external-json">Clase generada</label><textarea id="external-json" [(ngModel)]="json" (ngModelChange)="changed()" [disabled]="busy()" rows="8" maxlength="32001"></textarea>
<label for="external-file">O selecciona un archivo .json (máximo 128 KiB)</label><input id="external-file" type="file" accept=".json,application/json" (change)="file($event)" [disabled]="busy()"/>
<button type="button" (click)="validate()" [disabled]="busy()||!json.trim()">Validar y revisar</button>
@if(error()){<p class="notice danger" role="alert">{{error()}}</p>}
@if(preview();as p){
 @if(p.errors.length){<div class="notice warning" role="alert"><ul>@for(e of p.errors;track $index){<li>{{e}}</li>}</ul></div><button type="button" class="secondary" (click)="copy(p.correctionInstructions)">Copiar instrucciones de corrección</button><details><summary>Seleccionar instrucciones para copiar manualmente</summary><label for="correction-text">Instrucciones de corrección</label><textarea id="correction-text" readonly [value]="p.correctionInstructions"></textarea></details>}
 @if(p.content;as content){<h3>3. Revisar la clase</h3><p class="notice info">Formato validado. Revisa la corrección pedagógica antes de aprobar o publicar.</p>
 <app-lesson-summary [content]="content"/><ul>@for(w of p.warnings;track $index){<li>{{w}}</li>}</ul>
 <button type="button" (click)="save()" [disabled]="busy()">Guardar borrador y abrir constructor</button>}
}
<p role="status" aria-live="polite">{{status()}}</p></section>`})
export class LessonExternal {
 readonly prompt=input.required<string>();readonly back=output<void>();readonly busy=signal(false);readonly error=signal('');readonly status=signal('');readonly preview=signal<LessonImportPreview|null>(null);
 private readonly service=inject(LessonService);private readonly router=inject(Router);private readonly destroy=inject(DestroyRef);
 json='';private validatedJson='';private requestId=crypto.randomUUID();private submitted=false;
 changed(){this.preview.set(null);this.error.set('');this.status.set('');}
 async copy(text:string){try{await navigator.clipboard.writeText(text);this.status.set('Texto copiado.');}catch{this.error.set('No se pudo copiar. Selecciona el texto y cópialo con el teclado.');}}
 async file(event:Event){const file=(event.target as HTMLInputElement).files?.[0];if(!file||this.busy())return;this.changed();if(!file.name.toLowerCase().endsWith('.json')||file.size>131072){this.error.set('Selecciona un archivo .json de hasta 128 KiB.');return;}this.busy.set(true);try{const text=await file.text();if(text.length>32000){this.error.set('La clase supera el tamaño permitido (32000 caracteres).');return;}this.json=text;}catch{this.error.set('No se pudo leer el archivo. Puedes pegar la clase.');}finally{this.busy.set(false);}}
 validate(){if(this.busy())return;this.busy.set(true);this.error.set('');this.status.set('Validando…');this.service.previewImport(this.json).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:p=>{this.preview.set(p);this.busy.set(false);this.status.set(p.content?'Vista previa preparada. Todavía no se ha guardado.':'Corrige los errores y vuelve a importar.');if(this.validatedJson!==this.json){if(this.submitted)this.requestId=crypto.randomUUID();this.submitted=false;this.validatedJson=this.json;}},error:e=>{this.busy.set(false);this.status.set('');this.error.set(e.error?.title??'No se pudo validar. Conservamos el texto.');}});}
 save(){if(this.busy()||!this.preview()?.content||this.json!==this.validatedJson)return;this.busy.set(true);this.submitted=true;this.error.set('');this.service.importLesson(this.validatedJson,this.requestId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{void this.router.navigate(['/teacher/lessons',l.id]);},error:e=>{this.busy.set(false);this.error.set(e.error?.title??'No se confirmó el guardado. Reintenta con el mismo contenido para recuperar el borrador.');}});}
}

import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LessonService, emptyLesson } from './lesson.service';
@Component({selector:'app-lesson-create',imports:[FormsModule,RouterLink],styleUrl:'./lessons.css',template:`
<a routerLink="/teacher/lessons">← Mis clases</a>
<section class="creation-form"><p class="eyebrow">{{ai?'Con apoyo de IA':'Creación manual'}}</p><h1>{{ai?'Prepara tu clase con IA':'Comienza con una idea'}}</h1><p>{{ai?'Cuéntanos qué quieres enseñar. Podrás revisar y editar la propuesta.':'Guarda lo básico y añade el contenido a tu ritmo.'}}</p>
@if(error()){<p class="notice danger" role="alert">{{error()}}</p>}
<form #creationForm="ngForm" (ngSubmit)="create()"><fieldset [disabled]="busy()"><legend>Información básica</legend>
<label for="new-title">{{ai?'Tema de la clase':'Título'}}</label><input id="new-title" name="title" [(ngModel)]="title" maxlength="160" [required]="ai"/>
@if(ai){<label for="new-level">Nivel</label><select id="new-level" name="level" [(ngModel)]="level"><option>Principiante</option><option>Intermedio</option><option>Avanzado</option></select><label for="new-count">Cantidad aproximada de ejercicios</label><input id="new-count" name="count" type="number" min="1" max="24" required [(ngModel)]="count"/><label for="new-notes">Indicaciones adicionales (opcional)</label><textarea id="new-notes" name="notes" maxlength="500" [(ngModel)]="notes"></textarea>}
@else{<label for="new-subject">Asignatura o tema (opcional)</label><input id="new-subject" name="subject" maxlength="160" [(ngModel)]="subject"/><label for="new-description">Objetivo o descripción breve (opcional)</label><textarea id="new-description" name="description" maxlength="1000" [(ngModel)]="description"></textarea>}
<details><summary>Más opciones</summary>
@if(!ai){<label for="new-level">Nivel (opcional)</label><input id="new-level" name="level" [(ngModel)]="level" maxlength="100"/>}
<label for="new-duration">Duración estimada (minutos)</label><input id="new-duration" name="duration" required type="number" min="0" max="480" [(ngModel)]="duration"/>
@if(ai){<label for="new-goals">Objetivos deseados</label><textarea id="new-goals" name="goals" maxlength="200" [(ngModel)]="goals"></textarea><label for="new-types">Tipos de actividades</label><select id="new-types" name="types" [(ngModel)]="types"><option>Variados</option><option>Opción múltiple</option><option>Respuesta abierta</option><option>Código o SQL sin ejecución</option></select><label for="new-difficulty">Dificultad</label><select id="new-difficulty" name="difficulty" [(ngModel)]="difficulty"><option>Gradual</option><option>Básica</option><option>Desafiante</option></select><label for="new-hints">Cantidad orientativa de pistas por ejercicio</label><input id="new-hints" name="hints" required type="number" min="0" max="5" [(ngModel)]="hints"/><label for="new-rules">Reglas pedagógicas adicionales</label><textarea id="new-rules" name="rules" maxlength="200" [(ngModel)]="rules"></textarea>}
</details></fieldset>
<p class="note">{{ai?'Generar utiliza el proveedor configurado. La propuesta requiere tu revisión y aplicación.':'Puedes guardar un borrador incompleto y continuar después.'}}</p>
@if(creationForm.invalid&&creationForm.dirty){<p class="notice warning" role="status">Revisa los campos: tema obligatorio para IA, 1–24 ejercicios, 0–480 minutos y 0–5 pistas.</p>}<button type="submit" [disabled]="busy()||creationForm.invalid">{{busy()?'Guardando…':ai?'Generar con IA':'Crear borrador'}}</button><p role="status">{{status()}}</p></form></section>`})
export class LessonCreate {
 private readonly service=inject(LessonService);private readonly router=inject(Router);private readonly destroy=inject(DestroyRef);
 readonly ai=inject(ActivatedRoute).snapshot.data['mode']==='ai';readonly busy=signal(false);readonly error=signal('');readonly status=signal('');
 title='';subject='';description='';level=this.ai?'Principiante':'';count=3;notes='';duration=40;goals='';types='Variados';difficulty='Gradual';hints=2;rules='';private readonly requestId=crypto.randomUUID();
 create(){if(this.busy())return;if(!Number.isInteger(this.duration)||this.duration<0||this.duration>480||(this.ai&&(!this.title.trim()||!Number.isInteger(this.count)||this.count<1||this.count>24||!Number.isInteger(this.hints)||this.hints<0||this.hints>5))){this.error.set('Completa el tema y usa valores enteros: 1–24 ejercicios, 0–480 minutos y 0–5 pistas.');return;}this.busy.set(true);this.error.set(''); const prompt='Prepara una clase sobre '+this.title+'. Nivel: '+this.level+'. Aproximadamente '+this.count+' ejercicios. Duración: '+this.duration+' minutos. Dificultad: '+this.difficulty+'. Actividades: '+this.types+'. Pistas orientativas: '+this.hints+'. Objetivos: '+this.goals+'. Indicaciones: '+this.notes+'. Reglas: '+this.rules;const content={...emptyLesson(),title:this.title,subject:this.subject||this.title,description:this.description,level:this.level,durationMinutes:this.duration,instructions:this.ai?prompt:''};
 this.service.create(content,this.requestId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{
 if(!this.ai){void this.router.navigate(['/teacher/lessons',l.id]);return;}
 this.status.set('Borrador guardado. Generando propuesta…');

 this.service.design(l.id,crypto.randomUUID(),l.revision,'Plan',prompt).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{void this.router.navigate(['/teacher/lessons',l.id],{queryParams:{mode:'ai'}});},error:()=>{void this.router.navigate(['/teacher/lessons',l.id],{queryParams:{mode:'ai',generation:'check'}});}});
 },error:e=>{this.error.set(e.error?.title??'No se pudo guardar. Conservamos tus campos.');this.busy.set(false);}});}
}

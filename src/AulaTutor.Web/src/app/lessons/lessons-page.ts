import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { LessonService, LessonItem, emptyLesson, lessonState } from './lesson.service';
@Component({selector:'app-lessons-page',imports:[FormsModule,RouterLink,DatePipe],styleUrl:'./lessons.css',template:`
<section><h1>Mis clases</h1><p>Diseña, prueba y revisa tu contenido antes de aprobar una versión.</p><a routerLink="/teacher/assignments">Asignar clases publicadas a mis grupos</a>
@if(error()){<p role="alert" class="notice danger">{{error()}}</p>}
<form (ngSubmit)="create()"><h2>Crear clase</h2><p class="note">Puedes guardar un borrador incompleto y continuar después.</p>
<label for="new-title">Título</label><input id="new-title" name="title" [(ngModel)]="title" maxlength="160"/>
<label for="new-subject">Asignatura o tema</label><input id="new-subject" name="subject" [(ngModel)]="subject" maxlength="160"/>
<button [disabled]="busy()" type="submit">Crear borrador</button></form>
<h2>Clases guardadas</h2>@if(busy()){<p role="status">Cargando…</p>}
@for(l of lessons();track l.id){<article><h3><a [routerLink]="['/teacher/lessons',l.id]">{{l.title || 'Borrador sin título'}}</a></h3><p>{{state(l.state)}} · Modificada {{l.updatedAt|date:'dd/MM/yyyy HH:mm'}}</p><a [routerLink]="['/teacher/lessons',l.id]">Abrir clase</a></article>}@empty{<p>No tienes clases en esta página. Crea tu primer borrador.</p>}
<div class="actions"><button class="secondary" [disabled]="busy()||page===1" (click)="load(page-1)">Anterior</button><span>Página {{page}}</span><button class="secondary" [disabled]="busy()||lessons().length<50" (click)="load(page+1)">Siguiente</button></div>
</section>`})
export class LessonsPage {
  readonly service=inject(LessonService);private readonly router=inject(Router);private readonly destroy=inject(DestroyRef);
  readonly lessons=signal<LessonItem[]>([]);readonly busy=signal(false);readonly error=signal('');readonly state=lessonState;
  title='';subject='';page=1;private createKey=crypto.randomUUID();
  constructor(){this.load();}
  load(page=1){this.busy.set(true);this.error.set('');this.service.list(page).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{this.lessons.set(l);this.page=page;this.busy.set(false);},error:e=>{this.error.set(e.error?.title??'No se pudo consultar las clases.');this.busy.set(false);}});}
  create(){if(this.busy())return;this.busy.set(true);this.error.set('');this.service.create({...emptyLesson(),title:this.title,subject:this.subject},this.createKey).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{void this.router.navigate(['/teacher/lessons',l.id]);},error:e=>{this.error.set(e.error?.title??'No se pudo crear el borrador. Conservamos tus campos.');this.busy.set(false);}});}
}

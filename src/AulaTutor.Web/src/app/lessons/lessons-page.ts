import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { LessonService, LessonItem, lessonState } from './lesson.service';
@Component({selector:'app-lessons-page',imports:[FormsModule,RouterLink,DatePipe],styleUrl:'./lessons.css',template:`
<section class="library" aria-labelledby="library-title">
<header class="lesson-header"><div><p class="eyebrow">Tu espacio docente</p><h1 id="library-title">Mis clases</h1><p>Prepara tu próxima clase o retoma donde te quedaste.</p></div><a routerLink="/teacher/assignments">Ver asignaciones</a></header>
<div class="creation-grid">
<a class="creation-card" routerLink="/teacher/lessons/new/manual"><span class="creation-icon"><svg aria-hidden="true" viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M4 16 16 4l4 4L8 20H4v-4Zm10-10 4 4"/></svg></span><h2>Crear clase manualmente</h2><p>Diseña tu clase y sus actividades a tu manera</p><span>Comenzar →</span></a>
<a class="creation-card" routerLink="/teacher/lessons/new/ai"><span class="creation-icon"><svg aria-hidden="true" viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5L12 3Zm7-2v4m-2-2h4"/></svg></span><h2>Crear con apoyo de IA</h2><p>Prepara una clase con ayuda de inteligencia artificial</p><span>Preparar con IA →</span></a>
</div>
@if(error()){<p role="alert" class="notice danger">{{error()}}</p><button class="secondary" (click)="load(page)">Volver a cargar</button>}
<div class="lesson-header"><h2>Biblioteca de clases</h2>@if(!busy()&&!error()){<span class="note">{{lessons().length}} en esta página</span>}</div>
<div class="field-grid library-filters"><div><label for="lesson-search">Buscar en esta página</label><input id="lesson-search" type="search" [(ngModel)]="search" placeholder="Título de la clase"/></div><div><label for="lesson-state">Estado</label><select id="lesson-state" [(ngModel)]="filter"><option value="">Todos</option>@for(s of states;track s){<option [value]="s">{{state(s)}}</option>}</select></div></div>
@if(busy()){<p role="status">Cargando biblioteca…</p>}
<div class="lesson-grid" [attr.aria-busy]="busy()">
@for(l of filtered();track l.id){<article class="lesson-card"><span class="lesson-badge">{{state(l.state)}}</span><h3><a [routerLink]="['/teacher/lessons',l.id]">{{l.title || 'Borrador sin título'}}</a></h3><p class="note">Modificada {{l.updatedAt|date:'dd/MM/yyyy HH:mm'}}</p><a class="card-action" [routerLink]="['/teacher/lessons',l.id]">{{['Draft','Simulation','InReview'].includes(l.state)?'Continuar editando':'Ver clase y versiones'}} →</a></article>}
</div>
@if(!busy()&&!error()&&!filtered().length){<div class="library-empty"><h3>{{lessons().length?'No hay coincidencias':'Tu próxima clase comienza aquí'}}</h3><p>{{lessons().length?'Prueba otro título o estado en esta página.':'Elige crear manualmente o con apoyo de IA. Tus borradores se guardan para retomarlos después.'}}</p>@if(lessons().length){<button class="secondary" (click)="search='';filter=''">Limpiar filtros</button>}</div>}
<div class="actions"><button class="secondary" [disabled]="busy()||page===1" (click)="load(page-1)">Anterior</button><span>Página {{page}}</span><button class="secondary" [disabled]="busy()||lessons().length<50" (click)="load(page+1)">Siguiente</button></div>
</section>`})
export class LessonsPage {
 private readonly service=inject(LessonService);private readonly destroy=inject(DestroyRef);
 readonly lessons=signal<LessonItem[]>([]);readonly busy=signal(false);readonly error=signal('');readonly state=lessonState;
 readonly states=['Draft','Simulation','InReview','Approved','Published','Archived'];search='';filter='';page=1;
 constructor(){this.load();}
 filtered(){const query=this.search.trim().toLocaleLowerCase('es');return this.lessons().filter(l=>(!this.filter||l.state===this.filter)&&l.title.toLocaleLowerCase('es').includes(query));}
 load(page=1){if(this.busy())return;this.busy.set(true);this.error.set('');this.service.list(page).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:l=>{this.lessons.set(l);this.page=page;this.busy.set(false);},error:e=>{this.error.set(e.error?.title??'No se pudo consultar las clases.');this.busy.set(false);}});}
}

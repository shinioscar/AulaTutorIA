import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PreparedOptions, SessionItem, TutoringService } from './tutoring.service';
import { AppIcon } from '../shared/app-icon';

@Component({standalone:true,imports:[FormsModule,RouterLink,AppIcon],templateUrl:'./sessions-page.html',styleUrl:'./sessions-page.css'})
export class SessionsPage {
  private readonly api=inject(TutoringService); private readonly router=inject(Router);
  readonly sessions=signal<SessionItem[]>([]); readonly busy=signal(false); readonly message=signal('');
  topic=''; goal=''; level='Beginner'; directReview=false; page=1;
  experience='Tutoring';modality='Conversational';length='Short';readonly options=signal<PreparedOptions|null>(null);readonly optionsError=signal('');
  private creationKey=crypto.randomUUID();
  constructor(){ void this.load();void this.loadOptions(); }
  lengths(){const o=this.options();return this.experience==='Diagnostic'?o?.diagnostic??[]:o?.tutoring??[];}
  get canPrepare(){return this.lengths().some(o=>o.id===this.length&&o.available);}
  async loadOptions(){this.optionsError.set('');try{this.options.set(await firstValueFrom(this.api.preparedOptions()));}catch{this.optionsError.set('No se pudo comprobar el modelo. Revisa tu Proveedor de IA y vuelve a consultar.');}}
  label(state:string):string {return ({Created:'Por iniciar',Active:'Activa',Paused:'Pausada',Completed:'Finalizada'} as Record<string,string>)[state]??state;}
  async load(page=1):Promise<void> {
    this.busy.set(true);this.message.set('');
    try {this.sessions.set(await firstValueFrom(this.api.list(page)));this.page=page;}
    catch {this.message.set('No se pudieron cargar tus tutorías. Comprueba tu sesión.');}
    finally {this.busy.set(false);}
  }
  async create():Promise<void> {
    if(this.busy())return;this.busy.set(true);this.message.set('');
    try { const session=await firstValueFrom(this.api.create({requestId:this.creationKey,topic:this.topic,goal:this.goal.trim()||(this.experience==='Diagnostic'?'Explorar mis conocimientos de ':'Practicar ')+this.topic,level:this.level,directReview:this.directReview,experience:this.experience,modality:this.modality,length:this.length})); await this.router.navigate(['/tutoring',session.id]); }
    catch(error:unknown) {this.message.set((error as {error?:{title?:string}})?.error?.title??'No se pudo confirmar la creación. Actualiza el listado antes de intentar otra tutoría. Repetir los mismos datos conserva la clave de esta solicitud.');}
    finally {this.busy.set(false);}
  }
  newRequest():void {this.creationKey=crypto.randomUUID();this.message.set('Nueva solicitud preparada. Comprueba antes el listado para evitar crear otra sesión si la anterior sí se guardó.');}
}

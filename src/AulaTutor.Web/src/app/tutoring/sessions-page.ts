import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionItem, TutoringService } from './tutoring.service';

@Component({standalone:true,imports:[FormsModule,RouterLink],templateUrl:'./sessions-page.html'})
export class SessionsPage {
  private readonly api=inject(TutoringService); private readonly router=inject(Router);
  readonly sessions=signal<SessionItem[]>([]); readonly busy=signal(false); readonly message=signal('');
  topic=''; goal=''; level='Beginner'; directReview=false; page=1;
  private creationKey=crypto.randomUUID();
  constructor(){ void this.load(); }
  label(state:string):string {return ({Created:'Por iniciar',Active:'Activa',Paused:'Pausada',Completed:'Finalizada'} as Record<string,string>)[state]??state;}
  async load(page=1):Promise<void> {
    this.busy.set(true);this.message.set('');
    try {this.sessions.set(await firstValueFrom(this.api.list(page)));this.page=page;}
    catch {this.message.set('No se pudieron cargar tus tutorías. Comprueba tu sesión.');}
    finally {this.busy.set(false);}
  }
  async create():Promise<void> {
    if(this.busy())return;this.busy.set(true);this.message.set('');
    try { const session=await firstValueFrom(this.api.create({requestId:this.creationKey,topic:this.topic,goal:this.goal,level:this.level,directReview:this.directReview})); await this.router.navigate(['/tutoring',session.id]); }
    catch {this.message.set('No se pudo confirmar la creación. Actualiza el listado antes de intentar otra tutoría. Repetir los mismos datos conserva la clave de esta solicitud.');}
    finally {this.busy.set(false);}
  }
  newRequest():void {this.creationKey=crypto.randomUUID();this.message.set('Nueva solicitud preparada. Comprueba antes el listado para evitar crear otra sesión si la anterior sí se guardó.');}
}

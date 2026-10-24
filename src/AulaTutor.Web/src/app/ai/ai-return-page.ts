import { Component, OnDestroy, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';

@Component({standalone:true,imports:[RouterLink],template:`<section aria-live="polite"><p class="eyebrow">OPENROUTER</p><h1>{{title()}}</h1><p>{{message()}}</p><a class="button-link" routerLink="/ai-provider">Ir a Proveedor de IA</a></section>`})
export class AiReturnPage implements OnDestroy {
  readonly title=signal('Completando conexión…'); readonly message=signal('Espera un momento.'); readonly connected=signal(false);
  private readonly http=inject(HttpClient); private readonly auth=inject(AuthService); private readonly route=inject(ActivatedRoute); private destroyed=false;
  constructor(){void this.complete();}
  private async complete():Promise<void>{
    if(this.route.snapshot.queryParamMap.get('result')==='rejected'){this.title.set('Autorización no completada');this.message.set('Puedes volver a conectar desde Proveedor de IA.');return;}
    try{await this.auth.load();if(!this.auth.user())throw new Error('Session unavailable');await firstValueFrom(this.http.post('/api/ai-connections/openrouter/complete',{}));if(this.destroyed)return;
      this.title.set('OpenRouter conectado');this.message.set('Elige un modelo en Proveedor de IA para empezar.');this.connected.set(true);
      if(window.opener)window.opener.postMessage('aulatutor-ai-connected',window.location.origin);
    }catch(e){if(this.destroyed)return;this.title.set('No se completó la conexión');this.message.set(e instanceof HttpErrorResponse&&typeof e.error?.title==='string'?e.error.title:'Inicia sesión y vuelve a conectar OpenRouter.');}
  }
  ngOnDestroy():void{this.destroyed=true;}
}

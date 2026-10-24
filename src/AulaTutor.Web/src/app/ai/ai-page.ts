import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';

interface Configuration {
  provider: string; model: string; configurationValid: boolean;
  timeoutSeconds: number; maxConcurrency: number; maxRetries: number;
  capabilities: {json:boolean; jsonSchema:boolean; strictJsonSchema:boolean; streamingTransport:boolean; streamingImplemented:boolean};
}
interface Diagnostic {
  success: boolean; provider:string; model:string; durationMs:number; text:string|null;
  usage:{inputTokens:number;outputTokens:number;isEstimated:boolean}|null;
  errorCode:string|null; error:string|null; retryAfterSeconds:number|null;
}
@Component({standalone:true, templateUrl:'./ai-page.html', styleUrl:'./ai-page.css'})
export class AiPage {
  private readonly http = inject(HttpClient);
  readonly auth = inject(AuthService);
  readonly health = signal<{status:string;aiProvider:string}|null>(null);
  readonly connectionError = signal('');
  readonly configuration = signal<Configuration|null>(null);
  readonly result = signal<Diagnostic|null>(null);
  readonly busy = signal(false); readonly message = signal('');
  constructor() { void this.load(); }
  async load():Promise<void> {
    if(this.busy()) return;
    this.busy.set(true); this.message.set(''); this.connectionError.set(''); this.health.set(null); this.configuration.set(null); this.result.set(null);
    await Promise.all([
      firstValueFrom(this.http.get<{status:string;aiProvider:string}>('/api/health'))
        .then(health=>this.health.set(health))
        .catch(()=>this.connectionError.set('Conexión interrumpida. Intenta actualizar.')),
      this.auth.hasRole('Admin') ? firstValueFrom(this.http.get<Configuration>('/api/admin/ai/'))
        .then(configuration=>this.configuration.set(configuration))
        .catch(()=>this.message.set('No se pudo consultar la configuración. Actualiza tu sesión.')) : Promise.resolve(),
    ]);
    this.busy.set(false);
  }
  async test():Promise<void> {
    if (this.busy() || !this.auth.hasRole('Admin')) return;
    this.busy.set(true); this.result.set(null); this.message.set('');
    try { this.result.set(await firstValueFrom(this.http.post<Diagnostic>('/api/admin/ai/test', {}))); }
    catch { this.message.set('No se pudo completar la prueba. Comprueba tu sesión o espera antes de reintentar.'); }
    finally { this.busy.set(false); }
  }
}

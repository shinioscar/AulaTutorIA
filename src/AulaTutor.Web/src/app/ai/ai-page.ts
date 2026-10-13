import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

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
@Component({standalone:true, templateUrl:'./ai-page.html'})
export class AiPage {
  private readonly http = inject(HttpClient);
  readonly configuration = signal<Configuration|null>(null);
  readonly result = signal<Diagnostic|null>(null);
  readonly busy = signal(false); readonly message = signal('');
  constructor() { void this.load(); }
  async load():Promise<void> {
    this.busy.set(true); this.message.set('');
    try { this.configuration.set(await firstValueFrom(this.http.get<Configuration>('/api/admin/ai/'))); }
    catch { this.message.set('No se pudo consultar la configuración. Comprueba la sesión administrativa.'); }
    finally { this.busy.set(false); }
  }
  async test():Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.result.set(null); this.message.set('');
    try { this.result.set(await firstValueFrom(this.http.post<Diagnostic>('/api/admin/ai/test', {}))); }
    catch { this.message.set('No se pudo completar la prueba. Comprueba tu sesión o espera antes de reintentar.'); }
    finally { this.busy.set(false); }
  }
}

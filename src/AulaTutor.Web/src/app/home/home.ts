import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({ standalone: true, imports: [RouterLink], templateUrl: './home.html', styleUrl: './home.css' })
export class Home {
  private readonly http = inject(HttpClient);
  readonly auth = inject(AuthService);
  readonly status = signal('Comprobando conexión…'); readonly provider = signal<string | null>(null);
  readonly loading = signal(false);
  constructor() { this.checkHealth(); }
  checkHealth(): void {
    this.loading.set(true);
    this.http.get<{status:string;aiProvider:string}>('/api/health').subscribe({
      next: health => { this.status.set(health.status === 'ok' ? 'API disponible' : 'API sin disponibilidad'); this.provider.set(health.aiProvider); this.loading.set(false); },
      error: () => { this.status.set('No se pudo conectar con la API. Comprueba que esté iniciada.'); this.provider.set(null); this.loading.set(false); },
    });
  }
}

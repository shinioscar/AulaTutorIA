import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

interface ApiHealth {
  status: string;
  application: string;
  iteration: number;
  aiProvider: string;
  aiModel: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  templateUrl: './app.html',
})
export class App {
  private readonly http = inject(HttpClient);
  readonly status = signal('Comprobando conexión…');
  readonly provider = signal<string | null>(null);
  readonly loading = signal(false);

  constructor() {
    this.checkHealth();
  }

  checkHealth(): void {
    this.loading.set(true);
    this.http.get<ApiHealth>('/api/health').subscribe({
      next: (health) => {
        this.status.set(health.status === 'ok' ? 'API disponible' : 'API sin disponibilidad');
        this.provider.set(health.aiProvider);
        this.loading.set(false);
      },
      error: () => {
        this.status.set('No se pudo conectar con la API. Comprueba que esté iniciada.');
        this.provider.set(null);
        this.loading.set(false);
      },
    });
  }
}

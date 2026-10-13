import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { ThemeToggle } from './theme/theme-toggle';

@Component({
  selector: 'app-root', standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ThemeToggle], templateUrl: './app.html',
})
export class App {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly message = signal('');
  constructor() { void this.auth.load().catch(() => this.message.set('No se pudo conectar. Comprueba el servicio y actualiza la página.')); }
  async logout(): Promise<void> {
    try { await this.auth.logout(); await this.router.navigateByUrl('/login'); }
    catch { this.message.set('No se pudo cerrar la sesión. Comprueba la conexión e inténtalo nuevamente.'); }
  }
}

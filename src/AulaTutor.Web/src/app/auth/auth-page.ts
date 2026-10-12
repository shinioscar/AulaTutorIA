import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  standalone: true, imports: [FormsModule, RouterLink], templateUrl: './auth-page.html',
})
export class AuthPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly register = inject(ActivatedRoute).snapshot.data['register'] === true;
  email = ''; password = ''; displayName = ''; requestedRole = 'Student';
  readonly busy = signal(false); readonly message = signal('');
  async submit(): Promise<void> {
    this.busy.set(true); this.message.set('');
    try {
      if (this.register) {
        await this.auth.register(this.email, this.password, this.displayName, this.requestedRole);
        this.message.set('Solicitud recibida. Puedes iniciar sesión; si solicitaste acceso docente, espera la aprobación administrativa.');
      } else {
        await this.auth.login(this.email, this.password);
        await this.router.navigateByUrl('/');
      }
    } catch {
      this.message.set(this.register ? 'No se pudo registrar. Revisa los campos y los requisitos de contraseña o intenta más tarde.' : 'No se pudo iniciar sesión. Revisa las credenciales o intenta más tarde.');
    } finally { this.password = ''; this.busy.set(false); }
  }
}

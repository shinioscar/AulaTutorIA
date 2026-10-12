import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService, UserView } from '../auth/auth.service';

@Component({ standalone: true, templateUrl: './admin-page.html' })
export class AdminPage {
  private readonly http = inject(HttpClient); readonly auth = inject(AuthService);
  readonly users = signal<UserView[]>([]); readonly message = signal(''); readonly busy = signal(false);
  readonly roles = ['Admin', 'Teacher', 'Student']; page = 1;
  constructor() { void this.load(); }
  async load(page = this.page): Promise<void> {
    await this.run(async () => {
      const data = await firstValueFrom(this.http.get<{items:UserView[]}>(`/api/admin/users?page=${page}`));
      this.users.set(data.items); this.page = page;
    });
  }
  async decide(user: UserView, approve: boolean): Promise<void> {
    await this.run(async () => { await firstValueFrom(this.http.post(`/api/admin/users/${user.id}/teacher-decision`, {approve})); await this.load(); });
  }
  async toggleRole(user: UserView, role: string): Promise<void> {
    const roles = user.roles.includes(role) ? user.roles.filter(x => x !== role) : [...user.roles,role];
    if (role === 'Admin' && !window.confirm(`¿Confirmar el cambio del rol Admin de ${user.displayName}?`)) return;
    await this.run(async () => { await firstValueFrom(this.http.put(`/api/admin/users/${user.id}/roles`, {roles})); await this.load(); });
  }
  async toggleStatus(user: UserView): Promise<void> {
    await this.run(async () => { await firstValueFrom(this.http.put(`/api/admin/users/${user.id}/status`, {isActive:!user.isActive})); await this.load(); });
  }
  private async run(operation: () => Promise<void>): Promise<void> {
    this.busy.set(true); this.message.set('');
    try { await operation(); }
    catch (error) { this.message.set(error instanceof HttpErrorResponse ? error.error?.title ?? 'No se pudo completar el cambio.' : 'No se pudo completar el cambio.'); }
    finally { this.busy.set(false); }
  }
}

import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { catchError, firstValueFrom, throwError } from 'rxjs';
import { CanActivateFn, Router } from '@angular/router';

export interface UserView {
  id: string; displayName: string; email: string; organizationId: string;
  roles: string[]; teacherApproval: string; isActive: boolean;
}
export interface GroupView { id: string; name: string; isArchived: boolean; students: number; teachers: number; canViewMembers: boolean; }
export interface MemberView { userId: string; displayName: string; kind: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  readonly user = signal<UserView | null>(null);
  readonly ready = signal(false);
  private pending: Promise<void> | null = null;

  load(): Promise<void> {
    if (this.ready()) return Promise.resolve();
    return this.pending ??= this.refresh().finally(() => { this.pending = null; });
  }
  async refresh(): Promise<void> {
    try { this.user.set(await firstValueFrom(this.http.get<UserView>('/api/me'))); }
    catch { this.user.set(null); }
    await firstValueFrom(this.http.get('/api/auth/csrf'));
    this.ready.set(true);
  }
  async login(email: string, password: string): Promise<void> {
    await this.load();
    await firstValueFrom(this.http.post('/api/auth/login', { email, password }));
    await this.refresh(); // Rotate the identity-bound CSRF token after authentication.
  }
  async register(email: string, password: string, displayName: string, requestedRole: string): Promise<void> {
    await this.load();
    await firstValueFrom(this.http.post('/api/auth/register', { email, password, displayName, requestedRole }));
  }
  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/logout', {}));
    this.user.set(null);
    await this.refresh();
  }
  hasRole(role: string): boolean { return this.user()?.roles.includes(role) ?? false; }
}

export const signedInGuard: CanActivateFn = async () => {
  const auth = inject(AuthService); const router = inject(Router);
  await auth.load();
  return auth.user() ? true : router.createUrlTree(['/login']);
};
export const adminGuard: CanActivateFn = async () => {
  const auth = inject(AuthService); const router = inject(Router);
  await auth.load();
  return auth.hasRole('Admin') ? true : router.createUrlTree(['/']);
};
export const studentGuard: CanActivateFn = async () => {
  const auth=inject(AuthService); const router=inject(Router);
  await auth.load(); return auth.hasRole('Student')?true:router.createUrlTree(['/']);
};

export const sessionErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  return next(request).pipe(catchError(error => {
    if (error.status === 401 && !request.url.startsWith('/api/auth/login')) {
      auth.user.set(null);
      auth.ready.set(false);
    }
    return throwError(() => error);
  }));
};

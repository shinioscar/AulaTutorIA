import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService, GroupView, MemberView, UserView } from '../auth/auth.service';

@Component({ standalone: true, imports: [FormsModule], templateUrl: './groups-page.html' })
export class GroupsPage {
  private readonly http = inject(HttpClient); readonly auth = inject(AuthService);
  readonly groups = signal<GroupView[]>([]); readonly members = signal<MemberView[]>([]);
  readonly users = signal<UserView[]>([]); readonly selected = signal<GroupView | null>(null);
  readonly message = signal(''); readonly busy = signal(false);
  name = ''; memberId = ''; kind = 'Student'; rename = ''; userPage = 1;
  constructor() { void this.load(); }
  async load(): Promise<void> {
    try { this.groups.set(await firstValueFrom(this.http.get<GroupView[]>('/api/groups/'))); }
    catch { this.message.set('No se pudieron cargar tus grupos. Comprueba tu sesión y permisos.'); }
  }
  async select(group: GroupView): Promise<void> {
    this.selected.set(group); this.rename = group.name; this.members.set([]);
    if (group.canViewMembers) {
      await this.action(async () => { this.members.set(await firstValueFrom(this.http.get<MemberView[]>(`/api/groups/${group.id}/members`))); });
    }
    if (this.auth.hasRole('Admin')) await this.loadUsers(1);
  }
  async loadUsers(page: number): Promise<void> {
    await this.action(async () => {
      const data = await firstValueFrom(this.http.get<{items:UserView[]}>(`/api/admin/users?page=${page}`));
      this.users.set(data.items); this.userPage = page;
    });
  }
  async create(): Promise<void> {
    await this.action(async () => { await firstValueFrom(this.http.post('/api/groups/', { name: this.name })); this.name = ''; await this.load(); });
  }
  async renameGroup(): Promise<void> {
    const group = this.selected(); if (!group) return;
    await this.action(async () => { await firstValueFrom(this.http.put(`/api/groups/${group.id}`, {name:this.rename})); this.selected.set({...group,name:this.rename}); await this.load(); });
  }
  async archive(): Promise<void> {
    const group = this.selected(); if (!group) return;
    if (!window.confirm(`¿Archivar el grupo ${group.name}? Sus miembros perderán acceso al grupo.`)) return;
    await this.action(async () => { await firstValueFrom(this.http.delete(`/api/groups/${group.id}`)); this.selected.set(null); await this.load(); });
  }
  async add(): Promise<void> {
    const group = this.selected(); if (!group) return;
    await this.action(async () => { await firstValueFrom(this.http.put(`/api/groups/${group.id}/members`, {userId:this.memberId,kind:this.kind})); await this.select(group); await this.load(); });
  }
  async remove(member: MemberView): Promise<void> {
    const group = this.selected(); if (!group) return;
    await this.action(async () => { await firstValueFrom(this.http.delete(`/api/groups/${group.id}/members/${member.userId}/${member.kind}`)); await this.select(group); await this.load(); });
  }
  private async action(operation: () => Promise<void>): Promise<void> {
    this.busy.set(true); this.message.set('');
    try { await operation(); }
    catch (error) { this.message.set(error instanceof HttpErrorResponse ? error.error?.title ?? 'No se pudo completar el cambio.' : 'No se pudo completar el cambio.'); }
    finally { this.busy.set(false); }
  }
}

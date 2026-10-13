import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';
export const THEME_STORAGE_KEY = 'aulatutor.theme.v1';

@Injectable({providedIn: 'root'})
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly window = this.document.defaultView;
  private readonly current = signal<Theme>('light');
  readonly theme = this.current.asReadonly();
  readonly isDark = computed(() => this.current() === 'dark');

  constructor() {
    let stored: string | null = null;
    try { stored = this.window?.localStorage.getItem(THEME_STORAGE_KEY) ?? null; } catch { /* Storage can be unavailable. Theme remains usable. */ }
    this.apply(stored === 'dark' ? 'dark' : 'light');
    const synchronize = (event: StorageEvent): void => {
      if (event.key !== THEME_STORAGE_KEY && event.key !== null) return;
      this.apply(event.newValue === 'dark' ? 'dark' : 'light');
    };
    this.window?.addEventListener('storage', synchronize);
    inject(DestroyRef).onDestroy(() => this.window?.removeEventListener('storage', synchronize));
  }
  toggle(): void { this.set(this.isDark() ? 'light' : 'dark'); }
  set(theme: Theme): void {
    this.apply(theme);
    try { this.window?.localStorage.setItem(THEME_STORAGE_KEY, theme); } catch { /* The choice still applies for this page. */ }
  }
  private apply(theme: Theme): void {
    this.document.documentElement.setAttribute('data-theme', theme);
    this.current.set(theme);
  }
}

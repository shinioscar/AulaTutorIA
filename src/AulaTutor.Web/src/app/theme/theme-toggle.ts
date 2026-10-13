import { Component, inject } from '@angular/core';
import { ThemeService } from './theme.service';

@Component({selector:'app-theme-toggle',standalone:true,template:`
  <button type="button" class="secondary theme-toggle" aria-label="Tema oscuro"
    [attr.aria-pressed]="theme.isDark()" [title]="theme.isDark() ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'"
    (click)="theme.toggle()">
    <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7">
      @if(theme.isDark()){<path d="M20 15.2A8.5 8.5 0 0 1 8.8 4a8.5 8.5 0 1 0 11.2 11.2Z" />}
      @else{<circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M2 12h2m16 0h2M5 5l1.5 1.5m11 11L19 19M5 19l1.5-1.5m11-11L19 5"/>}
    </svg><span>Oscuro</span>
  </button>
`,styles:`.theme-toggle{display:inline-flex;align-items:center;gap:var(--space-2);font-size:var(--font-small);white-space:nowrap}.theme-toggle[aria-pressed='true']{background:var(--surface-secondary)}.theme-toggle svg{width:var(--icon-medium);height:var(--icon-medium)}`})
export class ThemeToggle {readonly theme = inject(ThemeService);}

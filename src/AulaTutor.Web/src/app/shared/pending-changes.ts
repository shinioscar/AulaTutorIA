import { CanDeactivateFn } from '@angular/router';

export interface PendingChanges { hasUnsavedChanges():boolean; }
export const pendingChangesGuard:CanDeactivateFn<PendingChanges> = component =>
  !component.hasUnsavedChanges() || window.confirm('Hay cambios sin guardar. ¿Quieres salir y descartarlos?');

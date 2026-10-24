import { Component, ElementRef, effect, input, output, viewChild } from '@angular/core';

@Component({selector:'app-detail-drawer',template:`
<dialog #panel tabindex="-1" [attr.aria-label]="label()" (keydown)="keepFocus($event)" (cancel)="$event.preventDefault(); dismissed.emit()">
  <header><h2>{{label()}}</h2><button type="button" class="secondary" (click)="dismissed.emit()">Cerrar</button></header>
  <div class="drawer-body"><ng-content /></div>
</dialog>`,styles:`
dialog{position:fixed;inset:0 0 0 auto;margin:0;width:min(38rem,100%);max-width:100%;height:100dvh;max-height:100dvh;padding:0;border:0;border-left:1px solid var(--border);background:var(--surface);color:var(--text-primary);box-shadow:var(--shadow-card)}
dialog[open]{display:flex;flex-direction:column;animation:drawer-enter var(--duration-standard) var(--ease-standard)}
@keyframes drawer-enter{from{opacity:0;transform:translateX(1rem)}to{opacity:1;transform:translateX(0)}}
dialog::backdrop{background:var(--text-primary);opacity:.3}
header{display:flex;align-items:center;justify-content:space-between;gap:var(--space-4);padding:var(--space-5);border-bottom:1px solid var(--border)}
h2{margin:0;font-size:var(--font-large)}
.drawer-body{overflow:auto;overscroll-behavior:contain;padding:var(--space-5);flex:1;min-height:0}
`})
export class DetailDrawer {
  readonly open=input(false);
  readonly label=input('Detalle');
  readonly dismissed=output<void>();
  private readonly panel=viewChild<ElementRef<HTMLDialogElement>>('panel');
  keepFocus(event:KeyboardEvent):void {
    if(event.key!=='Tab')return;
    const dialog=this.panel()?.nativeElement;if(!dialog)return;
    const items=Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled]),a[href],input:not([disabled]),select:not([disabled]),textarea:not([disabled]),summary,[tabindex]:not([tabindex="-1"])')).filter(item=>item.getClientRects().length>0);
    const first=items[0],last=items.at(-1),active=dialog.ownerDocument.activeElement;
    if(!first||!last){event.preventDefault();dialog.focus();return;}
    if(event.shiftKey&&(active===first||active===dialog)){event.preventDefault();last.focus();}
    else if(!event.shiftKey&&(active===last||active===dialog)){event.preventDefault();first.focus();}
  }
  constructor(){effect(()=>{const dialog=this.panel()?.nativeElement;if(!dialog)return;if(this.open()&&!dialog.open)dialog.showModal();else if(!this.open()&&dialog.open)dialog.close();});}
}

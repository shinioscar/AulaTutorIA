import { Component, input } from '@angular/core';

/** Local interface symbols, independent of academic content. */
@Component({selector:'app-icon',template:`<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path [attr.d]="paths[name()] || paths['book']"/></svg>`,styles:`:host{display:inline-flex;flex-shrink:0;width:1.35rem;height:1.35rem}svg{width:100%;height:100%}`})
export class AppIcon {
  readonly name=input('book');
  readonly paths:Record<string,string>={
    home:'m3 10 9-7 9 7M5 9v12h5v-7h4v7h5V9',
    book:'M12 6c-3-2-6-2-9-1v14c3-1 6-1 9 1 3-2 6-2 9-1V5c-3-1-6-1-9 1Zm0 0v14',
    chart:'M4 20V10m8 10V4m8 16v-7',
    assignment:'M8 3H5v18h14V3h-3M8 2h8v4H8V2Zm0 9h8m-8 5h5',
    users:'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m20 0v-2a4 4 0 0 0-3-4M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm8-8a4 4 0 0 1 0 8',
    chat:'M21 11a8 8 0 0 1-8 8H7l-5 3 2-6a8 8 0 1 1 17-5Z',
    settings:'M4 7h6m4 0h6M10 4h4v6h-4V4ZM4 17h10m4 0h2m-6-3h4v6h-4v-6Z',
    menu:'M4 6h16M4 12h16M4 18h16',
    refresh:'M20 7v5h-5M4 17v-5h5M6 7a7 7 0 0 1 12-1l2 3M4 15l2 3a7 7 0 0 0 12-1',
    sparkles:'m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5L12 3Zm7-1v4m-2-2h4',
    'arrow-right':'M4 12h16m-6-6 6 6-6 6',
  };
}

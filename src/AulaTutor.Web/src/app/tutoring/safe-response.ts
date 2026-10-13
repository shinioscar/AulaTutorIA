import { Component, Input } from '@angular/core';

interface Inline { text:string; code:boolean; }
interface Block { kind:'paragraph'|'code'|'list'; text:string; language:string; items:string[]; }
// Small display subset: Angular text interpolation only; never HTML/innerHTML or unsafe links.
@Component({selector:'app-safe-response',standalone:true,template:`
  @for (block of blocks; track $index) {
    @if (block.kind === 'code') { <pre [attr.aria-label]="block.language ? 'Código ' + block.language : 'Código'"><code>{{ block.text }}</code></pre> }
    @else if (block.kind === 'list') { <ul>@for (item of block.items; track $index) { <li>@for (part of inline(item); track $index) { @if (part.code) { <code>{{part.text}}</code> } @else { {{part.text}} } }</li> }</ul> }
    @else { <p>@for (part of inline(block.text); track $index) { @if (part.code) { <code>{{part.text}}</code> } @else { {{part.text}} } }</p> }
  }
`,styles:[`p {white-space:pre-wrap;margin:.4rem 0;line-height:1.65;overflow-wrap:anywhere} pre {white-space:pre;overflow:auto;max-width:100%;padding:1rem;background:#112c3b;color:#eef6fa;border-radius:.5rem;font-size:.9rem} code {font-family:ui-monospace,Consolas,monospace} p code,li code {background:#e5edf2;color:#143c4d;padding:.1rem .3rem;border-radius:.2rem} ul {padding-left:1.4rem} li {overflow-wrap:anywhere;line-height:1.65}`]})
export class SafeResponse {
  blocks:Block[]=[];
  @Input() set text(value:string) {
    this.blocks=[]; let buffer:string[]=[]; let code=false; let language=''; let list:string[]=[];
    const flush=()=>{ if(buffer.length) this.blocks.push({kind:code?'code':'paragraph',text:buffer.join('\n'),language,items:[]}); buffer=[]; if(list.length) this.blocks.push({kind:'list',text:'',language:'',items:list}); list=[]; };
    for(const line of value.replace(/\r\n/g,'\n').split('\n')) {
      if(line.startsWith('```')) {flush(); code=!code; language=code?line.slice(3).trim().slice(0,20):''; continue;}
      if(code) {buffer.push(line); continue;}
      if(/^\s*(?:[-*]|\d+\.)\s+/.test(line)) { if(buffer.length) flush(); list.push(line.replace(/^\s*(?:[-*]|\d+\.)\s+/,'')); continue; }
      if(list.length) flush();
      if(!line.trim()) {flush();continue;} buffer.push(line);
    }
    flush();
  }
  inline(text:string):Inline[] { return text.split(/(`[^`\n]+`)/g).filter(Boolean).map(part=>({code:part.startsWith('`')&&part.endsWith('`'),text:part.startsWith('`')&&part.endsWith('`')?part.slice(1,-1):part})); }
}

import { Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { AppIcon } from '../shared/app-icon';

export interface PersonalConnection { id:string; provider:string; fingerprint:string; status:string; method:string; updatedAt:string; preferredModel:string|null; }
export interface PersonalAiView { custodyAvailable:boolean; selection:string; connectionId:string|null; model:string|null; institutionalProvider:string; institutionalModel:string; connections:PersonalConnection[]; }
interface Model { id:string; name:string; compatible:boolean; free:boolean; priceKnown:boolean; availability:string; checkedAt:string; expiresAt:string; }
interface OAuthStart { authorizationUrl:string; expiresAt:string; }
const base='/api/ai-connections';

@Component({standalone:true,imports:[FormsModule,DatePipe,AppIcon],templateUrl:'./personal-ai-page.html',styleUrl:'./personal-ai-page.css'})
export class PersonalAiPage implements OnDestroy {
  private readonly http=inject(HttpClient);
  @ViewChild('keyInput') keyInput?:ElementRef<HTMLInputElement>;
  @ViewChild('modelInput') modelInput?:ElementRef<HTMLSelectElement>;
  @ViewChild('manualEditor') manualEditor?:ElementRef<HTMLElement>;
  @ViewChild('modelEditor') modelEditor?:ElementRef<HTMLElement>;
  readonly view=signal<PersonalAiView|null>(null);
  readonly busy=signal(false); readonly connecting=signal(false); readonly message=signal(''); readonly error=signal('');
  readonly manual=signal(''); readonly choosing=signal<PersonalConnection|null>(null); readonly models=signal<Model[]>([]);
  readonly redirectUrl=signal('');
  readonly providers=[{id:'Gemini',name:'Gemini',letter:'G',description:'Modelos de Google',console:'https://aistudio.google.com/api-keys'},
    {id:'Anthropic',name:'Claude',letter:'C',description:'Modelos de Anthropic',console:'https://platform.claude.com/settings/keys'},
    {id:'Groq',name:'Groq',letter:'G',description:'Inferencia con Groq',console:'https://console.groq.com/keys'}];
  apiKey=''; workspace=''; modelId=''; privacyAccepted=false; paidAccepted=false; includePaid=false;
  private popup:Window|null=null; private poll:ReturnType<typeof setInterval>|undefined; private pollBusy=false;
  private expiresAt=0; private beforeConnection=''; private destroyed=false;
  private readonly onMessage=(event:MessageEvent<unknown>):void=>{
    if(event.origin===window.location.origin && event.source===this.popup && event.data==='aulatutor-ai-connected') void this.pollConnection();
  };
  constructor(){window.addEventListener('message',this.onMessage);void this.load();}
  connection(provider:string):PersonalConnection|undefined{return this.view()?.connections.find(c=>c.provider===provider);}
  label(provider:string):string{return provider==='Anthropic'?'Claude':provider;}
  currentProvider():string { const v=this.view();return v?.selection==='Personal'?this.label(v.connections.find(c=>c.id===v.connectionId)?.provider??'Proveedor personal'):v?.selection==='None'?'Elige un proveedor':v?.institutionalProvider??'Cargando…'; }
  status(connection:PersonalConnection|undefined):string{return !connection||connection.status==='Disconnected'?'Sin conectar':connection.status==='Invalid'?'Vuelve a conectar':connection.method==='Manual'?'Clave guardada':'Conectado';}
  async load():Promise<void>{if(this.busy())return;this.busy.set(true);this.error.set('');this.view.set(null);try{this.view.set(await firstValueFrom(this.http.get<PersonalAiView>(base+'/')));}catch(e){this.error.set(this.safeError(e));}finally{this.busy.set(false);}}
  openManual(provider:string):void {this.manual.set(provider);this.choosing.set(null);this.apiKey='';this.workspace='';this.error.set('');this.message.set('');setTimeout(()=>{if(this.destroyed)return;this.manualEditor?.nativeElement.scrollIntoView({block:'start',behavior:'instant'});this.keyInput?.nativeElement.focus({preventScroll:true});});}
  closeManual():void{this.apiKey='';this.workspace='';this.manual.set('');}
  async saveManual():Promise<void>{
    if(this.busy()||this.apiKey.length<8)return;this.busy.set(true);this.error.set('');this.message.set('');
    const body={provider:this.manual(),apiKey:this.apiKey,workspace:this.workspace||null};this.apiKey='';
    try{await firstValueFrom(this.http.post(base+'/manual',body));this.closeManual();this.view.set(await firstValueFrom(this.http.get<PersonalAiView>(base+'/')));this.message.set('Clave guardada. Elige un modelo para activarla.');}
    catch(e){this.error.set(this.safeError(e));}finally{this.busy.set(false);}
  }
  async choose(connection:PersonalConnection,refresh=false):Promise<void>{
    if(this.busy())return;this.busy.set(true);this.error.set('');this.message.set('');this.closeManual();this.choosing.set(connection);this.models.set([]);this.privacyAccepted=false;this.paidAccepted=false;this.includePaid=false;this.modelId='';
    try{const models=await firstValueFrom(refresh?this.http.post<Model[]>(`${base}/models/${connection.provider}/refresh`,{}):this.http.get<Model[]>(`${base}/models/${connection.provider}`));this.models.set(models);
      this.modelId=models.find(m=>m.compatible&&m.free)?.id??(connection.provider==='OpenRouter'?'':models.find(m=>m.compatible)?.id??'');
      setTimeout(()=>{if(this.destroyed)return;this.modelEditor?.nativeElement.scrollIntoView({block:'start',behavior:'instant'});this.modelInput?.nativeElement.focus({preventScroll:true});});}
    catch(e){this.error.set(this.safeError(e));}finally{this.busy.set(false);}
  }
  visibleModels():Model[]{return this.models().filter(m=>this.choosing()?.provider!=='OpenRouter'||m.free||this.includePaid);}
  hasCompatibleModels():boolean{return this.visibleModels().some(m=>m.compatible);}
  selectedModel():Model|undefined{return this.models().find(m=>m.id===this.modelId);}
  async activate():Promise<void>{
    const c=this.choosing(),m=this.selectedModel();if(!c||!m?.compatible||!this.privacyAccepted||(!m.free&&!this.paidAccepted)||this.busy())return;
    this.busy.set(true);this.error.set('');try{await firstValueFrom(this.http.put(base+'/selection',{connectionId:c.id,model:m.id,privacyAccepted:true,paidAccepted:!m.free&&this.paidAccepted}));this.view.set(await firstValueFrom(this.http.get<PersonalAiView>(base+'/')));this.choosing.set(null);this.message.set(`${this.label(c.provider)} activado para nuevas consultas.`);}catch(e){this.error.set(this.safeError(e));}finally{this.busy.set(false);}
  }
  async institutional():Promise<void>{if(this.busy())return;this.busy.set(true);this.error.set('');try{await firstValueFrom(this.http.put(base+'/selection',{connectionId:null,model:null,privacyAccepted:false,paidAccepted:false}));this.view.set(await firstValueFrom(this.http.get<PersonalAiView>(base+'/')));this.choosing.set(null);this.message.set('Proveedor institucional seleccionado para nuevas consultas.');}catch(e){this.error.set(this.safeError(e));}finally{this.busy.set(false);}}
  async disconnect(c:PersonalConnection):Promise<void>{if(this.busy())return;this.busy.set(true);this.error.set('');try{await firstValueFrom(this.http.delete(base+'/'+c.id));this.view.set(await firstValueFrom(this.http.get<PersonalAiView>(base+'/')));if(this.choosing()?.id===c.id)this.choosing.set(null);this.message.set(`${this.label(c.provider)} desconectado de AulaTutor.`);}catch(e){this.error.set(this.safeError(e));}finally{this.busy.set(false);}}
  async connectOpenRouter():Promise<void>{
    if(this.busy()||this.connecting())return;this.error.set('');this.message.set('');this.redirectUrl.set('');
    const mobile=window.matchMedia('(max-width: 767px)').matches;
    this.popup=mobile?null:window.open('about:blank','aulatutor-ai-'+crypto.randomUUID(),'popup,width=600,height=760');
    if(this.popup){this.popup.document.title='Conectar OpenRouter';this.popup.document.body.textContent='Preparando autorización…';}
    this.busy.set(true);
    try{
      const start=await firstValueFrom(this.http.post<OAuthStart>(base+'/openrouter/start',{}));
      const url=new URL(start.authorizationUrl);if(url.origin!=='https://openrouter.ai'||url.pathname!=='/auth')throw new Error('Authorization destination rejected');
      this.beforeConnection=this.connection('OpenRouter')?.updatedAt??'';this.expiresAt=Date.parse(start.expiresAt);
      if(mobile){window.location.assign(url.href);return;}
      this.connecting.set(true);this.redirectUrl.set(url.href);
      if(this.popup){this.popup.opener=null;this.popup.location.replace(url.href);}
      this.poll=setInterval(()=>void this.pollConnection(),5000);
    }catch(e){this.popup?.close();this.popup=null;this.error.set(this.safeError(e));}finally{this.busy.set(false);}
  }
  continueRedirect():void{if(this.redirectUrl())window.location.assign(this.redirectUrl());}
  async cancelOAuth():Promise<void>{this.stopPolling();this.popup?.close();this.popup=null;this.connecting.set(false);this.redirectUrl.set('');try{await firstValueFrom(this.http.post(base+'/openrouter/cancel',{}));}catch{}this.message.set('Autorización cancelada. Puedes volver a conectar.');}
  private async pollConnection():Promise<void>{
    if(this.pollBusy||this.destroyed||!this.connecting())return;this.pollBusy=true;
    try{const view=await firstValueFrom(this.http.get<PersonalAiView>(base+'/'));if(this.destroyed)return;const c=view.connections.find(c=>c.provider==='OpenRouter');
      if(c?.status==='Connected'&&c.updatedAt!==this.beforeConnection){this.view.set(view);this.stopPolling();this.connecting.set(false);this.redirectUrl.set('');this.popup?.close();this.popup=null;this.message.set('OpenRouter conectado. Elige un modelo para activarlo.');}
      else if(Date.now()>=this.expiresAt){await this.cancelOAuth();}
    }catch(e){this.stopPolling();this.connecting.set(false);this.error.set(this.safeError(e));}finally{this.pollBusy=false;}
  }
  private stopPolling():void{if(this.poll)clearInterval(this.poll);this.poll=undefined;}
  private safeError(error:unknown):string{return error instanceof HttpErrorResponse&&typeof error.error?.title==='string'?error.error.title:'No se completó la operación. Actualiza el estado e inténtalo de nuevo.';}
  ngOnDestroy():void{this.destroyed=true;this.stopPolling();this.apiKey='';this.workspace='';window.removeEventListener('message',this.onMessage);}
}

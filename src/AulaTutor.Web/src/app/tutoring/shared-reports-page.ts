import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Report, TutoringService } from './tutoring.service';
import { ReportCard } from './report-card';
@Component({standalone:true,imports:[ReportCard],template:`<h1>Reportes compartidos contigo</h1><p>Solo versiones autorizadas expresamente por estudiantes con una relación académica vigente. El historial privado no está disponible.</p><button type="button" (click)="refresh()">Actualizar reportes</button><p role="status">{{message()}}</p>@for(r of reports();track r.id){<section><app-report-card [report]="r" /></section>}@empty{<p>No hay reportes autorizados disponibles.</p>}`})
export class SharedReportsPage {
  private readonly api=inject(TutoringService);readonly reports=signal<Report[]>([]);readonly message=signal('');
  constructor(){void this.refresh();}
  async refresh():Promise<void>{try{this.reports.set(await firstValueFrom(this.api.sharedReports()));this.message.set('');}catch{this.reports.set([]);this.message.set('No se pudieron consultar los reportes. Se requiere una cuenta docente aprobada.');}}
}

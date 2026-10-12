import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { App } from './app/app';
import { routes } from './app/app.routes';
import { sessionErrorInterceptor } from './app/auth/auth.service';

bootstrapApplication(App, { providers: [
  provideHttpClient(withXsrfConfiguration({cookieName:'XSRF-TOKEN',headerName:'X-XSRF-TOKEN'}), withInterceptors([sessionErrorInterceptor])),
  provideRouter(routes),
] }).catch(() => console.error('No se pudo iniciar AulaTutor IA.'));

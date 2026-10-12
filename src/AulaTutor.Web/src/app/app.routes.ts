import { Routes } from '@angular/router';
import { signedInGuard, adminGuard } from './auth/auth.service';

export const routes: Routes = [
  {path:'', loadComponent:() => import('./home/home').then(m => m.Home)},
  {path:'login', loadComponent:() => import('./auth/auth-page').then(m => m.AuthPage)},
  {path:'register', data:{register:true}, loadComponent:() => import('./auth/auth-page').then(m => m.AuthPage)},
  {path:'groups', canActivate:[signedInGuard], loadComponent:() => import('./groups/groups-page').then(m => m.GroupsPage)},
  {path:'admin/users', canActivate:[adminGuard], loadComponent:() => import('./admin/admin-page').then(m => m.AdminPage)},
  {path:'**', redirectTo:''},
];

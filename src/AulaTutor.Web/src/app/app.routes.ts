import { Routes } from '@angular/router';
import { signedInGuard, adminGuard, studentGuard } from './auth/auth.service';

export const routes: Routes = [
  {path:'tutoring',canActivate:[studentGuard],loadComponent:()=>import('./tutoring/sessions-page').then(m=>m.SessionsPage)},
  {path:'tutoring/:id',canActivate:[studentGuard],loadComponent:()=>import('./tutoring/chat-page').then(m=>m.ChatPage)},
  {path:'', loadComponent:() => import('./home/home').then(m => m.Home)},
  {path:'login', loadComponent:() => import('./auth/auth-page').then(m => m.AuthPage)},
  {path:'register', data:{register:true}, loadComponent:() => import('./auth/auth-page').then(m => m.AuthPage)},
  {path:'groups', canActivate:[signedInGuard], loadComponent:() => import('./groups/groups-page').then(m => m.GroupsPage)},
  {path:'admin/users', canActivate:[adminGuard], loadComponent:() => import('./admin/admin-page').then(m => m.AdminPage)},
  {path:'admin/ai', canActivate:[adminGuard], loadComponent:() => import('./ai/ai-page').then(m => m.AiPage)},
  {path:'**', redirectTo:''},
];

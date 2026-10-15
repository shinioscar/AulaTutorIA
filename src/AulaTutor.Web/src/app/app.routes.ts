import { Routes } from '@angular/router';
import { signedInGuard, adminGuard, studentGuard, teacherGuard } from './auth/auth.service';

export const routes: Routes = [
  {path:'teacher/assignments',canActivate:[teacherGuard],loadComponent:()=>import('./lessons/assignments-page').then(m=>m.AssignmentsPage)},
  {path:'classes/:id',canActivate:[studentGuard],loadComponent:()=>import('./lessons/student-classes-page').then(m=>m.StudentClassesPage)},
  {path:'classes',canActivate:[studentGuard],loadComponent:()=>import('./lessons/student-classes-page').then(m=>m.StudentClassesPage)},
  {path:'teacher/lessons/:id/simulations/:simulationId',canActivate:[teacherGuard],loadComponent:()=>import('./lessons/lesson-simulation').then(m=>m.LessonSimulation)},
  {path:'teacher/lessons/:id',canActivate:[teacherGuard],loadComponent:()=>import('./lessons/lesson-editor').then(m=>m.LessonEditor)},
  {path:'teacher/lessons',canActivate:[teacherGuard],loadComponent:()=>import('./lessons/lessons-page').then(m=>m.LessonsPage)},
  {path:'teacher/reports',canActivate:[signedInGuard],loadComponent:()=>import('./tutoring/shared-reports-page').then(m=>m.SharedReportsPage)},
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

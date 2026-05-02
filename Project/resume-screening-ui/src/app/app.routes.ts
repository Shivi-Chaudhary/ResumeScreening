import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { guestGuard } from './core/guest.guard';
import { hrAdminGuard } from './core/hr-admin.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'jobs' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'jobs',
    canActivate: [authGuard],
    loadComponent: () => import('./jobs/job-list/job-list.component').then((m) => m.JobListComponent),
  },
  {
    path: 'jobs/new',
    canActivate: [authGuard, hrAdminGuard],
    loadComponent: () => import('./jobs/job-create/job-create.component').then((m) => m.JobCreateComponent),
  },
  {
    path: 'jobs/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./jobs/job-detail/job-detail.component').then((m) => m.JobDetailComponent),
  },
  { path: '**', redirectTo: 'jobs' },
];

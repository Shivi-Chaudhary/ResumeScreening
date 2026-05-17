import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

const TOKEN_KEY = 'rs_token';
const USER_KEY = 'rs_user';
const API = environment.apiUrl;

export interface AuthResponse {
  token: string;
  fullName: string;
  email: string;
  role: string;
  expiry: string;
}

export interface StoredUser {
  fullName: string;
  email: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API}/api/auth/login`, { email, password })
      .pipe(tap((r) => this.persist(r)));
  }

  register(body: {
    fullName: string;
    email: string;
    password: string;
    role: string;
  }): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API}/api/auth/register`, body)
      .pipe(tap((r) => this.persist(r)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    void this.router.navigateByUrl('/login');
  }

  isLoggedIn(): boolean {
    return !!localStorage.getItem(TOKEN_KEY);
  }

  isHrAdmin(): boolean {
    return this.getUser()?.role === 'HRAdmin';
  }

  isViewer(): boolean {
    return this.getUser()?.role === 'Viewer';
  }

  getUser(): StoredUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as StoredUser;
    } catch {
      return null;
    }
  }

  private persist(r: AuthResponse): void {
    const raw = r as AuthResponse & { Token?: string; Role?: string; FullName?: string; Email?: string };
    const token = raw.token ?? raw.Token;
    if (!token) return;
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(
      USER_KEY,
      JSON.stringify({
        fullName: raw.fullName ?? raw.FullName ?? '',
        email: raw.email ?? raw.Email ?? '',
        role: raw.role ?? raw.Role ?? 'Viewer',
      }),
    );
  }
}

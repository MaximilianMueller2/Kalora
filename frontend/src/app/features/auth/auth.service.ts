import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AntiforgeryService } from '../../core/http/antiforgery.service';
import { ApiClient } from '../../core/http/api-client';
import { LanguageService } from '../../core/i18n/language.service';
import { InvitationPreview, LoginRequest, Me, RegisterRequest, Role } from './auth.models';

/**
 * Who is signed in. The session itself is the HttpOnly cookie; this service only mirrors `GET /api/me` into signals.
 * All requests here are `silent`: the pages show the error codes inline instead of a snackbar.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly antiforgery = inject(AntiforgeryService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);

  private readonly user = signal<Me | null>(null);
  private loading: Promise<void> | undefined;

  readonly currentUser = this.user.asReadonly();
  readonly isLoggedIn = computed(() => this.user() !== null);
  readonly roles = computed<readonly Role[]>(() => {
    const user = this.user();
    return user
      ? [
          ...(user.role ? [user.role] : []),
          ...(user.isPlatformAdmin ? (['PlatformAdmin'] as const) : []),
        ]
      : [];
  });

  /** Loads the session once; guards await it so the first navigation knows whether somebody is signed in. */
  ensureLoaded(): Promise<void> {
    return (this.loading ??= this.loadCurrentUser());
  }

  hasAnyRole(allowed: readonly Role[]): boolean {
    const mine = this.roles();
    return allowed.some((role) => mine.includes(role));
  }

  async login(request: LoginRequest): Promise<Me> {
    return this.signedIn(
      await firstValueFrom(this.api.post<Me>('/auth/login', request, { silent: true })),
    );
  }

  async register(request: RegisterRequest): Promise<Me> {
    return this.signedIn(
      await firstValueFrom(this.api.post<Me>('/auth/register', request, { silent: true })),
    );
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.api.post<void>('/auth/logout'));
    this.clear();
    await this.refreshAntiforgery();
  }

  /** The server no longer knows this session (expired or ended elsewhere): back to the login page, then return here. */
  async sessionExpired(): Promise<void> {
    if (!this.isLoggedIn()) {
      return;
    }
    this.clear();
    await this.router.navigate(['/auth/login'], {
      queryParams: { returnUrl: this.router.url, reason: 'expired' },
    });
  }

  confirmEmail(userId: string, token: string): Promise<void> {
    return firstValueFrom(
      this.api.post<void>('/auth/confirm-email', { userId, token }, { silent: true }),
    );
  }

  forgotPassword(email: string): Promise<void> {
    return firstValueFrom(
      this.api.post<void>('/auth/forgot-password', { email }, { silent: true }),
    );
  }

  resetPassword(email: string, token: string, newPassword: string): Promise<void> {
    return firstValueFrom(
      this.api.post<void>('/auth/reset-password', { email, token, newPassword }, { silent: true }),
    );
  }

  getInvitation(token: string): Promise<InvitationPreview> {
    return firstValueFrom(
      this.api.get<InvitationPreview>(`/public/invitations/${encodeURIComponent(token)}`, {
        silent: true,
      }),
    );
  }

  async acceptInvitation(token: string, password: string): Promise<Me> {
    const body = { password, language: this.language.current() };
    return this.signedIn(
      await firstValueFrom(
        this.api.post<Me>(`/public/invitations/${encodeURIComponent(token)}/accept`, body, {
          silent: true,
        }),
      ),
    );
  }

  private async loadCurrentUser(): Promise<void> {
    try {
      const me = await firstValueFrom(this.api.get<Me>('/me', { silent: true }));
      this.user.set(me);
      await this.language.applyProfileLanguage(me.preferredLanguage);
    } catch {
      // 401 means nobody is signed in; an unreachable server looks the same to the UI (the login page then reports it).
      this.user.set(null);
    }
  }

  private async signedIn(me: Me): Promise<Me> {
    this.user.set(me);
    this.loading = Promise.resolve();
    await this.refreshAntiforgery();
    await this.language.applyProfileLanguage(me.preferredLanguage);
    return me;
  }

  private clear(): void {
    this.user.set(null);
    this.loading = Promise.resolve();
  }

  // Antiforgery tokens belong to one user; a new one avoids a rejected first request after login or logout.
  private async refreshAntiforgery(): Promise<void> {
    try {
      await firstValueFrom(this.antiforgery.refresh());
    } catch {
      // The retry interceptor fetches it on demand anyway.
    }
  }
}

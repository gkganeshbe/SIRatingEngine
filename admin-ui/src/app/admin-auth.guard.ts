import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { environment } from '../environments/environment';
import { AuthService } from './core/services/auth.service';

/**
 * Enforces authentication checking on Admin UI routes to prevent unauthorized
 * access to configuration pages. Protects against UI bypass in production.
 *
 * In non-production environments the guard is bypassed so the app can be used
 * without an OIDC provider being available locally.
 */
export const adminAuthGuard: CanActivateFn = (route, state) => {
  // Skip auth entirely in development — avoids requiring a local OIDC server.
  if (!environment.production) return true;

  const router = inject(Router);
  const authService = inject(AuthService);

  const isAuthenticated = authService.isAuthenticated();

  if (isAuthenticated) {
    // Additional validation (e.g. JWT expiration check) should occur here
    return true;
  }

  // Redirect to login, preserving the intended destination
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
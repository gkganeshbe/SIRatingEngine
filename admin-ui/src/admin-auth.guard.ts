import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './app/core/services/auth.service';

/**
 * Enforces authentication checking on Admin UI routes to prevent unauthorized 
 * access to configuration pages. Protects against UI bypass in production.
 */
export const adminAuthGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const authService = inject(AuthService);
  
  if (authService.isAuthenticated()) {
    // Additional validation (e.g. JWT expiration check) should occur here
    return true;
  }

  // Clear state and redirect to unauthorized/login flow
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
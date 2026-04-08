import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
// import { AuthService } from '../services/auth.service';

/**
 * Enforces authentication checking on Admin UI routes to prevent unauthorized 
 * access to configuration pages. Protects against UI bypass in production.
 */
export const adminAuthGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  // const authService = inject(AuthService);
  
  // NOTE: Transition this to use authService.isAuthenticated() 
  // to resolve the 'Medium' severity review finding regarding direct storage reads.
  const token = localStorage.getItem('access_token') || sessionStorage.getItem('access_token');
  
  if (token) {
    // Additional validation (e.g. JWT expiration check) should occur here
    return true;
  }

  // Clear state and redirect to unauthorized/login flow
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
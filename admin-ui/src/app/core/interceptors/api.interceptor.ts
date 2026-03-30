import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

const TENANT_KEY = 're_tenant_id';
const DEFAULT_TENANT = environment.tenants[0]?.id ?? '';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Only intercept calls to our API
  if (!req.url.startsWith(environment.apiUrl)) return next(req);

  // Read tenant from localStorage directly (same source as TenantService)
  const tenantId = localStorage.getItem(TENANT_KEY) || DEFAULT_TENANT;

  // Read bearer token from OAuthService's storage key (angular-oauth2-oidc stores it here)
  const token = localStorage.getItem('access_token') ?? sessionStorage.getItem('access_token');

  const headers: Record<string, string> = {
    'X-Tenant-Id': tenantId,
  };

  if (token) headers['Authorization'] = `Bearer ${token}`;

  return next(req.clone({ setHeaders: headers }));
};

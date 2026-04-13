import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { TenantService } from '../services/tenant.service';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const tenantService = inject(TenantService);

  const token = authService.getAccessToken();
  const tenantId = tenantService.tenantId;

  let headers = req.headers;
  
  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }
  
  if (tenantId) {
    headers = headers.set('X-Tenant-Id', tenantId);
  }

  const clonedReq = req.clone({ headers });
  return next(clonedReq);
};
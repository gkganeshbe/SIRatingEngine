import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class TenantService {
  readonly tenants = environment.tenants;

  private _tenantId: string = (() => {
    const stored = localStorage.getItem('re_tenant_id');
    const knownIds = environment.tenants.map(t => t.id);
    // If the stored value is no longer in the known tenant list, discard it
    return stored && knownIds.includes(stored) ? stored : (environment.tenants[0]?.id ?? '');
  })();

  get tenantId(): string { return this._tenantId; }

  setTenantId(id: string): void {
    this._tenantId = id;
    localStorage.setItem('re_tenant_id', id);
  }
}

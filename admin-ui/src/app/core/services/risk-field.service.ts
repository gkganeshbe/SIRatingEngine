import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RiskField, CreateRiskFieldRequest, UpdateRiskFieldRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class RiskFieldService {
  private base = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  /** Returns global (system) + product-specific fields for the given productCode. */
  list(productCode: string): Observable<RiskField[]> {
    return this.http.get<RiskField[]>(`${this.base}/products/${encodeURIComponent(productCode)}/risk-fields`);
  }

  /** Creates a field scoped to the product. The productCode is bound server-side from the route. */
  create(productCode: string, req: CreateRiskFieldRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(
      `${this.base}/products/${encodeURIComponent(productCode)}/risk-fields`, req);
  }

  update(id: number, req: UpdateRiskFieldRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/risk-fields/${id}`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/risk-fields/${id}`);
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CoverageSummary, CoverageDetail,
  CreateCoverageRequest, UpdateCoverageRequest, ExpireRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class CoverageService {
  private base = `${environment.apiUrl}/admin/coverages`;

  constructor(private http: HttpClient) {}

  list(productCode?: string): Observable<CoverageSummary[]> {
    let params = new HttpParams();
    if (productCode) params = params.set('productCode', productCode);
    return this.http.get<CoverageSummary[]>(this.base, { params });
  }

  get(productCode: string, coverageCode: string, version: string): Observable<CoverageDetail> {
    return this.http.get<CoverageDetail>(`${this.base}/${productCode}/${coverageCode}/${version}`);
  }

  create(req: CreateCoverageRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base, req);
  }

  update(id: number, req: UpdateCoverageRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, req);
  }

  expire(id: number, expireAt: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/expire`, { expireAt } as ExpireRequest);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}

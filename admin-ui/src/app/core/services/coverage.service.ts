import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CoverageSummary, CoverageDetail,
  CreateCoverageRequest, UpdateCoverageRequest, ExpireRequest,
  AddCoverageRefRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class CoverageService {
  private base = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  /** Lists state configs for a coverage catalog entry. */
  list(coverageRefId: number): Observable<CoverageSummary[]> {
    return this.http.get<CoverageSummary[]>(`${this.base}/coverages`, {
      params: new HttpParams().set('coverageRefId', coverageRefId)
    });
  }

  /** Lists all state configs for every coverage in a product (by manifest ID). */
  listByProduct(productManifestId: number): Observable<CoverageSummary[]> {
    return this.http.get<CoverageSummary[]>(`${this.base}/coverages`, {
      params: new HttpParams().set('productManifestId', productManifestId)
    });
  }

  /** Lists all state configs, optionally filtered by productCode string. */
  listAll(productCode?: string): Observable<CoverageSummary[]> {
    let params = new HttpParams();
    if (productCode) params = params.set('productCode', productCode);
    return this.http.get<CoverageSummary[]>(`${this.base}/coverages`, { params });
  }

  /** Gets a single state config (pipeline + rate tables) by its DB id. */
  get(id: number): Observable<CoverageDetail> {
    return this.http.get<CoverageDetail>(`${this.base}/coverages/${id}`);
  }

  create(req: CreateCoverageRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.base}/coverages`, req);
  }

  update(id: number, req: UpdateCoverageRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/coverages/${id}`, req);
  }

  expire(id: number, expireAt: string): Observable<void> {
    return this.http.post<void>(`${this.base}/coverages/${id}/expire`, { expireAt } as ExpireRequest);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/coverages/${id}`);
  }

  /** Adds a coverage type to a product's catalog (creates CoverageRef). */
  addToCatalog(manifestId: number, req: AddCoverageRefRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.base}/products/${manifestId}/catalog`, req);
  }

  /** Removes a coverage type from a product's catalog (cascades all state configs). */
  removeFromCatalog(coverageRefId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/catalog/${coverageRefId}`);
  }
}

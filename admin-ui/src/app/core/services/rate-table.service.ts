import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  RateTableSummary, RateTableDetail, RateTableRowDetail,
  CreateRateTableRequest, UpdateRateTableRequest,
  CreateRateTableRowRequest, ExpireRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class RateTableService {
  private base(coverageId: number) {
    return `${environment.apiUrl}/admin/coverages/${coverageId}/rate-tables`;
  }

  constructor(private http: HttpClient) {}

  list(coverageId: number): Observable<RateTableSummary[]> {
    return this.http.get<RateTableSummary[]>(this.base(coverageId));
  }

  get(coverageId: number, name: string): Observable<RateTableDetail> {
    return this.http.get<RateTableDetail>(`${this.base(coverageId)}/${name}`);
  }

  create(coverageId: number, req: CreateRateTableRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base(coverageId), req);
  }

  update(coverageId: number, id: number, req: UpdateRateTableRequest): Observable<void> {
    return this.http.put<void>(`${this.base(coverageId)}/${id}`, req);
  }

  delete(coverageId: number, id: number): Observable<void> {
    return this.http.delete<void>(`${this.base(coverageId)}/${id}`);
  }

  // ── Rows ──────────────────────────────────────────────────────────────────

  getRows(coverageId: number, name: string, effectiveDate?: string): Observable<RateTableRowDetail[]> {
    let params = new HttpParams();
    if (effectiveDate) params = params.set('effectiveDate', effectiveDate);
    return this.http.get<RateTableRowDetail[]>(`${this.base(coverageId)}/${name}/rows`, { params });
  }

  addRow(coverageId: number, name: string, req: CreateRateTableRowRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.base(coverageId)}/${name}/rows`, req);
  }

  bulkInsertRows(coverageId: number, name: string, rows: CreateRateTableRowRequest[]): Observable<{ inserted: number }> {
    return this.http.post<{ inserted: number }>(`${this.base(coverageId)}/${name}/rows/bulk`, { rows });
  }

  updateRow(coverageId: number, name: string, rowId: number, req: CreateRateTableRowRequest): Observable<void> {
    return this.http.put<void>(`${this.base(coverageId)}/${name}/rows/${rowId}`, req);
  }

  expireRow(coverageId: number, name: string, rowId: number, expireAt: string): Observable<void> {
    return this.http.post<void>(`${this.base(coverageId)}/${name}/rows/${rowId}/expire`, { expireAt } as ExpireRequest);
  }

  deleteRow(coverageId: number, name: string, rowId: number): Observable<void> {
    return this.http.delete<void>(`${this.base(coverageId)}/${name}/rows/${rowId}`);
  }
}

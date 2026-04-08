import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LobScopeDetail, UpdateCoverageRefRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class LobScopeService {
  private base = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  listScopes(lobId: number): Observable<LobScopeDetail[]> {
    return this.http.get<LobScopeDetail[]>(`${this.base}/lobs/${lobId}/scopes`);
  }

  addScope(lobId: number, scope: string): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.base}/lobs/${lobId}/scopes`, { scope });
  }

  deleteScope(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/scopes/${id}`);
  }

  updateCoverageAggregation(coverageRefId: number, req: UpdateCoverageRefRequest): Observable<void> {
    return this.http.patch<void>(`${this.base}/catalog/${coverageRefId}/aggregation`, req);
  }
}

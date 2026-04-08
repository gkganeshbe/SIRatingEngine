import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  LookupDimensionSummary, LookupDimensionDetail,
  CreateLookupDimensionRequest, UpdateLookupDimensionRequest, CreateLookupValueRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class LookupDimensionService {
  private base = `${environment.apiUrl}/admin/lookup-dimensions`;

  constructor(private http: HttpClient) {}

  list(productManifestId?: number): Observable<LookupDimensionSummary[]> {
    const params = productManifestId != null
      ? new HttpParams().set('productManifestId', productManifestId)
      : new HttpParams();
    return this.http.get<LookupDimensionSummary[]>(this.base, { params });
  }

  get(id: number): Observable<LookupDimensionDetail> {
    return this.http.get<LookupDimensionDetail>(`${this.base}/${id}`);
  }

  create(req: CreateLookupDimensionRequest, productManifestId?: number): Observable<{ id: number }> {
    const params = productManifestId != null
      ? new HttpParams().set('productManifestId', productManifestId)
      : new HttpParams();
    return this.http.post<{ id: number }>(this.base, req, { params });
  }

  update(id: number, req: UpdateLookupDimensionRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  addValue(dimensionId: number, req: CreateLookupValueRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.base}/${dimensionId}/values`, req);
  }

  deleteValue(valueId: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/admin/lookup-dimension-values/${valueId}`);
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DerivedKeyDetail, CreateDerivedKeyRequest, UpdateDerivedKeyRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class DerivedKeyService {
  private base = `${environment.apiUrl}/admin/derived-keys`;

  constructor(private http: HttpClient) {}

  list(productManifestId?: number): Observable<DerivedKeyDetail[]> {
    const params = productManifestId != null
      ? new HttpParams().set('productManifestId', productManifestId)
      : new HttpParams();
    return this.http.get<DerivedKeyDetail[]>(this.base, { params });
  }

  get(id: number): Observable<DerivedKeyDetail> {
    return this.http.get<DerivedKeyDetail>(`${this.base}/${id}`);
  }

  create(req: CreateDerivedKeyRequest, productManifestId?: number): Observable<{ id: number }> {
    const params = productManifestId != null
      ? new HttpParams().set('productManifestId', productManifestId)
      : new HttpParams();
    return this.http.post<{ id: number }>(this.base, req, { params });
  }

  update(id: number, req: UpdateDerivedKeyRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}

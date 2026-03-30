import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ProductSummary, ProductDetail,
  CreateProductRequest, UpdateProductRequest, ExpireRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private base = `${environment.apiUrl}/admin/products`;

  constructor(private http: HttpClient) {}

  list(): Observable<ProductSummary[]> {
    return this.http.get<ProductSummary[]>(this.base);
  }

  get(productCode: string, version: string): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`${this.base}/${productCode}/${version}`);
  }

  create(req: CreateProductRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base, req);
  }

  update(id: number, req: UpdateProductRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, req);
  }

  expire(id: number, expireAt: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/expire`, { expireAt } as ExpireRequest);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}

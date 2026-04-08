import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ProductStateDetail, AddProductStateRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ProductStateService {
  private base = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  list(manifestId: number): Observable<ProductStateDetail[]> {
    return this.http.get<ProductStateDetail[]>(`${this.base}/products/${manifestId}/states`);
  }

  add(manifestId: number, stateCode: string): Observable<{ id: number }> {
    const req: AddProductStateRequest = { stateCode: stateCode.toUpperCase().trim() };
    return this.http.post<{ id: number }>(`${this.base}/products/${manifestId}/states`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/states/${id}`);
  }
}

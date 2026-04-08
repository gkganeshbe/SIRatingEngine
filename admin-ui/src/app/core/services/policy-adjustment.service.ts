import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PolicyAdjustmentDetail,
  CreatePolicyAdjustmentRequest,
  UpdatePolicyAdjustmentRequest,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PolicyAdjustmentService {
  private base = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  list(manifestId: number): Observable<PolicyAdjustmentDetail[]> {
    return this.http.get<PolicyAdjustmentDetail[]>(
      `${this.base}/products/${manifestId}/adjustments`
    );
  }

  create(manifestId: number, req: CreatePolicyAdjustmentRequest): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(
      `${this.base}/products/${manifestId}/adjustments`, req
    );
  }

  update(id: number, req: UpdatePolicyAdjustmentRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/adjustments/${id}`, req);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/adjustments/${id}`);
  }
}

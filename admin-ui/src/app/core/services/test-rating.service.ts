import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminTestRateRequest, AdminTestRateResponse } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class TestRatingService {
  constructor(private http: HttpClient) {}

  rate(req: AdminTestRateRequest): Observable<AdminTestRateResponse> {
    return this.http.post<AdminTestRateResponse>(`${environment.apiUrl}/admin/test/rate`, req);
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserSummary, CreateUserRequest, ResetPasswordRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class UserService {
  private base = `${environment.apiUrl}/admin/users`;

  constructor(private http: HttpClient) {}

  list(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(this.base);
  }

  create(req: CreateUserRequest): Observable<UserSummary> {
    return this.http.post<UserSummary>(this.base, req);
  }

  delete(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(userId)}`);
  }

  resetPassword(userId: string, req: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(userId)}/reset-password`, req);
  }
}

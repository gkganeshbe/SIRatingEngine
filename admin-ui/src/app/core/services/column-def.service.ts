import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ColumnDefDetail, ColumnDefRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ColumnDefService {
  private base(tableName: string) {
    return `${environment.apiUrl}/admin/rate-tables/${tableName}/column-defs`;
  }

  constructor(private http: HttpClient) {}

  list(tableName: string): Observable<ColumnDefDetail[]> {
    return this.http.get<ColumnDefDetail[]>(this.base(tableName));
  }

  replace(tableName: string, defs: ColumnDefRequest[]): Observable<void> {
    return this.http.put<void>(this.base(tableName), defs);
  }

  update(tableName: string, id: number, def: ColumnDefRequest): Observable<void> {
    return this.http.put<void>(`${this.base(tableName)}/${id}`, def);
  }

  delete(tableName: string, id: number): Observable<void> {
    return this.http.delete<void>(`${this.base(tableName)}/${id}`);
  }
}

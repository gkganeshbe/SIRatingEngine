import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StepConfig, AddPipelineStepRequest, ReorderStepsRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PipelineService {
  private base(coverageId: number) {
    return `${environment.apiUrl}/admin/coverages/${coverageId}/pipeline`;
  }

  constructor(private http: HttpClient) {}

  listSteps(coverageId: number): Observable<StepConfig[]> {
    return this.http.get<StepConfig[]>(`${this.base(coverageId)}/steps`);
  }

  addStep(coverageId: number, step: StepConfig, insertAfterOrder?: number | null): Observable<{ dbId: number; stepId: string }> {
    const body: AddPipelineStepRequest = { step, insertAfterOrder: insertAfterOrder ?? null };
    return this.http.post<{ dbId: number; stepId: string }>(`${this.base(coverageId)}/steps`, body);
  }

  updateStep(coverageId: number, stepId: string, step: StepConfig): Observable<void> {
    return this.http.put<void>(`${this.base(coverageId)}/steps/${stepId}`, step);
  }

  deleteStep(coverageId: number, stepId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(coverageId)}/steps/${stepId}`);
  }

  reorderSteps(coverageId: number, orderedStepIds: string[]): Observable<void> {
    const body: ReorderStepsRequest = { orderedStepIds };
    return this.http.put<void>(`${this.base(coverageId)}/reorder`, body);
  }
}

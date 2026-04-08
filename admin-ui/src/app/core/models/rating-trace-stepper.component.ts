import { Component, Input } from '@angular/core';
import { RatingTraceResult } from '../../../../../core/models/api.models';

@Component({
  selector: 'app-rating-trace-stepper',
  templateUrl: './rating-trace-stepper.component.html'
})
export class RatingTraceStepperComponent {
  @Input() traces: RatingTraceResult[] = [];

  /**
   * Helper to check if an object dictionary has actual entries to display.
   */
  hasData(data: Record<string, string> | null | undefined): boolean {
    return !!data && Object.keys(data).length > 0;
  }
}
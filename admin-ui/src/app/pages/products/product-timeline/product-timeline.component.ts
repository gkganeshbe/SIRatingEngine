import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { ProductSummary } from '../../../core/models/api.models';
import { isExpired, ACTIVE_EXPIRE } from '../../../core/utils/date.utils';

/** ProductSummary enriched with a derived display status. */
export interface VersionNode extends ProductSummary {
  status: 'Active' | 'Future' | 'Expired';
}

@Component({
  selector: 'app-product-timeline',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  template: `
    <div class="timeline-wrapper">
      <div class="timeline-track"></div>

      <div class="timeline-nodes">
        <div *ngFor="let v of sortedVersions"
             class="timeline-node-container"
             [class.selected]="v.id === selectedVersionId"
             (click)="selectVersion(v)">

          <div class="timeline-node" [ngClass]="'node-' + (v.status | lowercase)">
            <mat-icon *ngIf="v.status === 'Active'">check_circle</mat-icon>
            <mat-icon *ngIf="v.status === 'Future'">schedule</mat-icon>
            <mat-icon *ngIf="v.status === 'Expired'">history</mat-icon>
          </div>

          <div class="timeline-content">
            <div class="timeline-version">v{{ v.version }}</div>
            <div class="timeline-date">{{ v.effStart | date:'mediumDate' }}</div>
            <div class="timeline-status" [ngClass]="'text-' + (v.status | lowercase)">
              {{ v.status }}
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ProductTimelineComponent implements OnChanges {
  @Input() versions: ProductSummary[] = [];
  @Input() selectedVersionId: number | null = null;
  @Output() versionSelected = new EventEmitter<ProductSummary>();

  sortedVersions: VersionNode[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['versions']) this.processVersions();
  }

  private processVersions(): void {
    const today = new Date().toISOString().slice(0, 10);
    this.sortedVersions = [...this.versions]
      .sort((a, b) => a.effStart.localeCompare(b.effStart))
      .map(v => {
        let status: VersionNode['status'];
        if (isExpired(v.expireAt))       status = 'Expired';
        else if (v.effStart > today)     status = 'Future';
        else                             status = 'Active';
        return { ...v, status };
      });
  }

  selectVersion(v: VersionNode): void {
    this.versionSelected.emit(v);
  }
}

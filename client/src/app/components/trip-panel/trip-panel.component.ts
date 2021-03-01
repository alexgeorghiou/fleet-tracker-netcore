import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { Trip } from '../../models/fleet.models';
import { FleetApiService } from '../../services/fleet-api.service';
import { VehicleView } from '../../services/fleet-store.service';

@Component({
  selector: 'ft-trip-panel',
  templateUrl: './trip-panel.component.html',
  styleUrls: ['./trip-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TripPanelComponent implements OnChanges {
  @Input() vehicle: VehicleView | null = null;
  @Input() activeTripId: string | null = null;
  @Output() tripSelected = new EventEmitter<string | null>();
  @Output() closed = new EventEmitter<void>();

  trips$: Observable<Trip[]> = of([]);
  private loadedFor: string | null = null;

  constructor(private readonly api: FleetApiService) {}

  ngOnChanges(changes: SimpleChanges): void {
    const id = this.vehicle ? this.vehicle.id : null;
    // Reload when the selection changes or when a trip opens/closes for the selected vehicle.
    const tripChanged = changes.vehicle && changes.vehicle.previousValue && this.vehicle &&
      changes.vehicle.previousValue.openTripId !== this.vehicle.openTripId;

    if (id !== this.loadedFor || tripChanged) {
      this.loadedFor = id;
      this.trips$ = id ? this.api.trips(id).pipe(catchError(() => of([]))) : of([]);
    }
  }

  toggle(trip: Trip): void {
    this.tripSelected.emit(this.activeTripId === trip.id ? null : trip.id);
  }

  trackById(_: number, t: Trip): string {
    return t.id;
  }
}

import { ChangeDetectionStrategy, Component, OnDestroy, OnInit } from '@angular/core';
import { BehaviorSubject, Observable, of, Subscription } from 'rxjs';
import { catchError, distinctUntilChanged, map, switchMap } from 'rxjs/operators';

import { Alert, Ping } from './models/fleet.models';
import { FleetApiService } from './services/fleet-api.service';
import { FleetStore } from './services/fleet-store.service';
import { LiveFleetService } from './services/live-fleet.service';

@Component({
  selector: 'ft-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnInit, OnDestroy {
  readonly vehicles$ = this.store.vehicles$;
  readonly geofences$ = this.store.geofences$;
  readonly alerts$ = this.store.alerts$;
  readonly selected$ = this.store.selected$;
  readonly selectedId$ = this.store.selectedId$;
  readonly counts$ = this.store.counts$;
  readonly connection$ = this.live.state$;

  readonly activeTripId$ = new BehaviorSubject<string | null>(null);
  readonly tripPath$: Observable<Ping[] | null> = this.activeTripId$.pipe(
    distinctUntilChanged(),
    switchMap(id => id
      ? this.api.trip(id).pipe(map(t => t.path), catchError(() => of(null)))
      : of(null))
  );

  private readonly subs = new Subscription();

  constructor(
    private readonly store: FleetStore,
    private readonly live: LiveFleetService,
    private readonly api: FleetApiService
  ) {}

  ngOnInit(): void {
    this.store.init();
    // Clear the trip overlay when the selection changes.
    this.subs.add(this.store.selectedId$.pipe(distinctUntilChanged()).subscribe(() => this.activeTripId$.next(null)));
  }

  select(vehicleId: string | null): void {
    this.store.select(vehicleId);
  }

  acknowledge(alert: Alert): void {
    this.store.acknowledge(alert);
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }
}

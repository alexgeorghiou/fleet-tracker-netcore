import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, combineLatest, Observable, Subscription } from 'rxjs';
import { map } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import { Alert, Geofence, LatLng, PositionUpdate, Vehicle, VehicleStatus } from '../models/fleet.models';
import { FleetApiService } from './fleet-api.service';
import { LiveFleetService } from './live-fleet.service';

export interface VehicleView extends Vehicle {
  trail: LatLng[];
}

const MOVING_THRESHOLD_KPH = 5;
const MAX_ALERTS = 100;

/** Derive status client-side from a live ping so the list updates without a REST round-trip. */
export function statusFromPing(p: PositionUpdate): VehicleStatus {
  if (!p.ignitionOn) {
    return 'Parked';
  }
  return p.speedKph >= MOVING_THRESHOLD_KPH ? 'Moving' : 'Idle';
}

/** Append to a bounded trail, skipping exact duplicates (parked vehicles keep reporting the same fix). */
export function appendTrail(trail: LatLng[], point: LatLng, max: number): LatLng[] {
  const last = trail.length ? trail[trail.length - 1] : null;
  if (last && last.lat === point.lat && last.lng === point.lng) {
    return trail;
  }
  const next = trail.concat([point]);
  return next.length > max ? next.slice(next.length - max) : next;
}

/**
 * Single source of truth for the dashboard: seeded from REST, then kept current by hub messages.
 * Uses immutable updates so OnPush components re-render only when their slice changes.
 */
@Injectable({ providedIn: 'root' })
export class FleetStore implements OnDestroy {
  private readonly vehicles = new BehaviorSubject<{ [id: string]: VehicleView }>({});
  private readonly geofences = new BehaviorSubject<Geofence[]>([]);
  private readonly alerts = new BehaviorSubject<Alert[]>([]);
  private readonly selectedId = new BehaviorSubject<string | null>(null);
  private readonly subs = new Subscription();

  readonly vehicles$: Observable<VehicleView[]> = this.vehicles.pipe(
    map(byId => Object.keys(byId).map(k => byId[k]).sort((a, b) => a.name.localeCompare(b.name)))
  );
  readonly geofences$ = this.geofences.asObservable();
  readonly alerts$ = this.alerts.asObservable();
  readonly selectedId$ = this.selectedId.asObservable();
  readonly selected$: Observable<VehicleView | null> = combineLatest([this.vehicles, this.selectedId]).pipe(
    map(([byId, id]) => (id && byId[id]) || null)
  );
  readonly counts$ = this.vehicles$.pipe(
    map(list => ({
      total: list.length,
      moving: list.filter(v => v.status === 'Moving').length,
      idle: list.filter(v => v.status === 'Idle').length,
      parked: list.filter(v => v.status === 'Parked' || v.status === 'Offline' || v.status === 'Unknown').length
    }))
  );

  constructor(private readonly api: FleetApiService, private readonly live: LiveFleetService) {}

  init(): void {
    this.subs.add(this.api.vehicles().subscribe(list => {
      const byId: { [id: string]: VehicleView } = {};
      for (const v of list) {
        const trail = v.lastPosition ? [{ lat: v.lastPosition.latitude, lng: v.lastPosition.longitude }] : [];
        byId[v.id] = Object.assign({}, v, { trail });
      }
      this.vehicles.next(byId);
    }));
    this.subs.add(this.api.geofences().subscribe(g => this.geofences.next(g)));
    this.subs.add(this.api.alerts().subscribe(a => this.alerts.next(a)));

    this.subs.add(this.live.positions$.subscribe(p => this.applyPosition(p)));
    this.subs.add(this.live.alerts$.subscribe(a => this.alerts.next([a].concat(this.alerts.value).slice(0, MAX_ALERTS))));
    this.subs.add(this.live.trips$.subscribe(t => {
      const current = this.vehicles.value[t.vehicleId];
      if (current) {
        const openTripId = t.status === 'InProgress' ? t.tripId : null;
        this.patch(t.vehicleId, Object.assign({}, current, { openTripId }));
      }
    }));

    this.live.start();
  }

  select(vehicleId: string | null): void {
    this.selectedId.next(this.selectedId.value === vehicleId ? null : vehicleId);
  }

  acknowledge(alert: Alert): void {
    this.api.acknowledge(alert.id).subscribe(() =>
      this.alerts.next(this.alerts.value.filter(a => a.id !== alert.id))
    );
  }

  private applyPosition(p: PositionUpdate): void {
    const current = this.vehicles.value[p.vehicleId];
    if (!current) {
      // A vehicle registered after page load: pick it up on the next refresh of the list.
      this.subs.add(this.api.vehicles().subscribe(list => {
        const v = list.find(x => x.id === p.vehicleId);
        if (v) {
          this.patch(v.id, Object.assign({}, v, { trail: [] }));
          this.applyPosition(p);
        }
      }));
      return;
    }

    const point = { lat: p.latitude, lng: p.longitude };
    this.patch(p.vehicleId, Object.assign({}, current, {
      status: statusFromPing(p),
      openTripId: p.tripId,
      lastPosition: {
        timestampUtc: p.timestampUtc,
        latitude: p.latitude,
        longitude: p.longitude,
        speedKph: p.speedKph,
        headingDegrees: p.headingDegrees,
        ignitionOn: p.ignitionOn
      },
      trail: appendTrail(current.trail, point, environment.trailLength)
    }));
  }

  private patch(id: string, vehicle: VehicleView): void {
    const next = Object.assign({}, this.vehicles.value);
    next[id] = vehicle;
    this.vehicles.next(next);
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }
}

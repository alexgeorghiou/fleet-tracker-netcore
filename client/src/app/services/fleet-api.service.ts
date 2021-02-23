import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { Alert, Geofence, Ping, Trip, TripDetail, Vehicle } from '../models/fleet.models';

@Injectable({ providedIn: 'root' })
export class FleetApiService {
  private readonly base = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  vehicles(): Observable<Vehicle[]> {
    return this.http.get<Vehicle[]>(`${this.base}/vehicles`);
  }

  geofences(): Observable<Geofence[]> {
    return this.http.get<Geofence[]>(`${this.base}/geofences`);
  }

  alerts(unacknowledgedOnly = true, take = 50): Observable<Alert[]> {
    const params = new HttpParams()
      .set('unacknowledgedOnly', String(unacknowledgedOnly))
      .set('take', String(take));
    return this.http.get<Alert[]>(`${this.base}/alerts`, { params });
  }

  acknowledge(alertId: string): Observable<Alert> {
    return this.http.post<Alert>(`${this.base}/alerts/${alertId}/acknowledge`, null);
  }

  trips(vehicleId: string, take = 20): Observable<Trip[]> {
    const params = new HttpParams().set('vehicleId', vehicleId).set('take', String(take));
    return this.http.get<Trip[]>(`${this.base}/trips`, { params });
  }

  trip(tripId: string): Observable<TripDetail> {
    return this.http.get<TripDetail>(`${this.base}/trips/${tripId}`);
  }

  recentPings(vehicleId: string, minutes = 30): Observable<Ping[]> {
    const from = new Date(Date.now() - minutes * 60000).toISOString();
    const params = new HttpParams().set('from', from);
    return this.http.get<Ping[]>(`${this.base}/vehicles/${vehicleId}/pings`, { params });
  }
}

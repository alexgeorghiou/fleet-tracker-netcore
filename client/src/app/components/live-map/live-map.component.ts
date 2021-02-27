import {
  AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, EventEmitter, Input, NgZone,
  OnChanges, OnDestroy, Output, SimpleChanges, ViewChild
} from '@angular/core';
import * as L from 'leaflet';

import { Geofence, LatLng, Ping } from '../../models/fleet.models';
import { VehicleView } from '../../services/fleet-store.service';

const STATUS_COLORS: { [status: string]: string } = {
  Moving: '#2e7d32',
  Idle: '#f9a825',
  Parked: '#546e7a',
  Offline: '#9e9e9e',
  Unknown: '#9e9e9e'
};

const DEFAULT_CENTER: L.LatLngExpression = [52.37, 4.89];

/**
 * Leaflet map rendered outside Angular's zone (Leaflet fires a lot of DOM events we never bind to).
 * Layers are diffed by id so a position update moves an existing marker instead of rebuilding the map.
 */
@Component({
  selector: 'ft-live-map',
  template: '<div #map class="map"></div>',
  styleUrls: ['./live-map.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LiveMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() vehicles: VehicleView[] = [];
  @Input() geofences: Geofence[] = [];
  @Input() selectedId: string | null = null;
  @Input() tripPath: Ping[] | null = null;
  @Output() vehicleSelected = new EventEmitter<string>();

  @ViewChild('map', { static: true }) mapElement: ElementRef<HTMLDivElement>;

  private map: L.Map;
  private readonly markers = new Map<string, L.Marker>();
  private readonly trails = new Map<string, L.Polyline>();
  private fenceLayer = L.layerGroup();
  private tripLayer = L.layerGroup();
  private fittedToFences = false;

  constructor(private readonly zone: NgZone) {}

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      this.map = L.map(this.mapElement.nativeElement, { zoomControl: true }).setView(DEFAULT_CENTER, 12);
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
      }).addTo(this.map);
      this.fenceLayer.addTo(this.map);
      this.tripLayer.addTo(this.map);
    });
    this.renderFences();
    this.renderVehicles();
    this.renderTrip();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.map) {
      return;
    }
    if (changes.geofences) {
      this.renderFences();
    }
    if (changes.vehicles || changes.selectedId) {
      this.renderVehicles();
    }
    if (changes.selectedId && this.selectedId) {
      this.panToSelected();
    }
    if (changes.tripPath) {
      this.renderTrip();
    }
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
    }
  }

  private renderFences(): void {
    this.zone.runOutsideAngular(() => {
      this.fenceLayer.clearLayers();
      const bounds = L.latLngBounds([]);
      for (const fence of this.geofences) {
        const latLngs = fence.vertices.map(v => L.latLng(v.lat, v.lng));
        const polygon = L.polygon(latLngs, {
          color: fence.color,
          weight: 2,
          fillOpacity: fence.isActive ? 0.12 : 0.03,
          dashArray: fence.isActive ? undefined : '4 4'
        }).bindTooltip(fence.name, { sticky: true });
        polygon.addTo(this.fenceLayer);
        latLngs.forEach(ll => bounds.extend(ll));
      }
      if (!this.fittedToFences && bounds.isValid()) {
        this.map.fitBounds(bounds.pad(0.2));
        this.fittedToFences = true;
      }
    });
  }

  private renderVehicles(): void {
    this.zone.runOutsideAngular(() => {
      const seen = new Set<string>();

      for (const v of this.vehicles) {
        if (!v.lastPosition) {
          continue;
        }
        seen.add(v.id);
        const pos = L.latLng(v.lastPosition.latitude, v.lastPosition.longitude);
        const selected = v.id === this.selectedId;
        const icon = this.iconFor(v, selected);

        let marker = this.markers.get(v.id);
        if (!marker) {
          marker = L.marker(pos, { icon, riseOnHover: true })
            .on('click', () => this.zone.run(() => this.vehicleSelected.emit(v.id)))
            .addTo(this.map);
          this.markers.set(v.id, marker);
        } else {
          marker.setLatLng(pos);
          marker.setIcon(icon);
        }
        marker.setZIndexOffset(selected ? 1000 : 0);
        if (marker.getTooltip()) {
          marker.setTooltipContent(this.tooltipFor(v));
        } else {
          marker.bindTooltip(this.tooltipFor(v), { direction: 'top', offset: L.point(0, -12) });
        }

        this.renderTrail(v.id, v.trail, STATUS_COLORS[v.status] || '#1565c0', selected);
      }

      // Remove vehicles that disappeared (deleted).
      this.markers.forEach((marker, id) => {
        if (!seen.has(id)) {
          marker.remove();
          this.markers.delete(id);
          const trail = this.trails.get(id);
          if (trail) {
            trail.remove();
            this.trails.delete(id);
          }
        }
      });
    });
  }

  private renderTrail(id: string, trail: LatLng[], color: string, selected: boolean): void {
    const latLngs = trail.map(p => L.latLng(p.lat, p.lng));
    let line = this.trails.get(id);
    if (!line) {
      line = L.polyline(latLngs, { color, weight: 3, opacity: 0.5 }).addTo(this.map);
      this.trails.set(id, line);
    } else {
      line.setLatLngs(latLngs);
    }
    line.setStyle({ color, opacity: selected ? 0.9 : 0.45, weight: selected ? 4 : 3 });
  }

  private renderTrip(): void {
    this.zone.runOutsideAngular(() => {
      this.tripLayer.clearLayers();
      if (!this.tripPath || this.tripPath.length < 2) {
        return;
      }
      const latLngs = this.tripPath.map(p => L.latLng(p.latitude, p.longitude));
      const line = L.polyline(latLngs, { color: '#6a1b9a', weight: 5, opacity: 0.8 }).addTo(this.tripLayer);
      L.circleMarker(latLngs[0], { radius: 6, color: '#2e7d32', fillOpacity: 1 }).bindTooltip('Start').addTo(this.tripLayer);
      L.circleMarker(latLngs[latLngs.length - 1], { radius: 6, color: '#c62828', fillOpacity: 1 }).bindTooltip('End').addTo(this.tripLayer);
      this.map.fitBounds(line.getBounds().pad(0.2));
    });
  }

  private panToSelected(): void {
    const marker = this.selectedId ? this.markers.get(this.selectedId) : undefined;
    if (marker) {
      this.zone.runOutsideAngular(() => this.map.panTo(marker.getLatLng()));
    }
  }

  private iconFor(v: VehicleView, selected: boolean): L.DivIcon {
    const color = STATUS_COLORS[v.status] || '#1565c0';
    const heading = v.lastPosition && v.lastPosition.headingDegrees != null ? v.lastPosition.headingDegrees : 0;
    const ring = selected ? '<circle cx="13" cy="13" r="12" fill="none" stroke="#1565c0" stroke-width="2"/>' : '';
    const shape = v.status === 'Moving'
      ? `<path d="M13 3 L20 21 L13 17 L6 21 Z" fill="${color}" stroke="#fff" stroke-width="1.5"/>`
      : `<circle cx="13" cy="13" r="7" fill="${color}" stroke="#fff" stroke-width="2"/>`;

    return L.divIcon({
      className: 'vehicle-marker',
      iconSize: [26, 26],
      iconAnchor: [13, 13],
      html: `<svg class="vehicle-marker__arrow" viewBox="0 0 26 26" style="transform: rotate(${heading}deg)">${ring}${shape}</svg>`
    });
  }

  private tooltipFor(v: VehicleView): string {
    const p = v.lastPosition;
    const speed = p ? `${Math.round(p.speedKph)} km/h` : '';
    return `<strong>${escapeHtml(v.name)}</strong> &middot; ${v.status}<br>${speed}`;
  }
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' } as any)[c]);
}

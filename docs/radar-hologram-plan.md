# Radar Hologram Feature Plan

## Overview

Add a radar-style holographic overlay to the HoloLens 1 airplane viewer:

- A **radar disc** hovering above the SETTINGS text hologram
- **Dotted lines** from the radar to each airplane with **distance labels** (meters/km)
- A **floor compass** under the settings/GPS/airplane list with N/S/E/W arrows
- A **"Set North" calibration** option in settings to compensate for magnetic declination

## Current Architecture

| Component | File | Role |
|---|---|---|
| `AirplaneRenderer` | `Content/AirplaneRenderer.cs` | D3D11 rendering of airplane cubes, labels, debug panel, SETTINGS button |
| `CompassService` | `Services/CompassService.cs` | Wraps `Windows.Devices.Sensors.Compass`, exposes `CurrentHeadingDegrees` |
| `AirplaneMath` | `Services/AirplaneMath.cs` | WGS-84 → dome coordinate mapping |
| `LocationOverrideStore` | `Services/LocationOverrideStore.cs` | Thread-safe manual location override |
| `BasicHologramMain` | `BasicHologramMain.cs` | App lifecycle, MessageDialog-based settings modal |

## Feature Details

### 1. Radar Hologram Disc

- Position: `settingsButtonPosition + (0, +0.35f, 0)` — 35cm above SETTINGS label
- Visual: flat disc with rotating sweep line (animated via `StepTimer`)
- Color: cyan/teal semi-transparent ring with radial grid lines

### 2. Dotted Lines + Distance Labels

- For each visible airplane, draw a dotted line from radar center to airplane world position
- Implementation: series of small cube segments with gaps (reuses `DrawCubeAt` pipeline)
- Distance label at midpoint: `AirplaneMath.GreatCircleDistanceMeters()` → "850 m" or "1.2 km"
- Labels are billboards facing the camera (reuse `DrawTextBillboard`)

### 3. Floor Compass

- Flat disc at `y = worldCenter.Y - 0.5f`, radius ~0.8m
- Four arrow glyphs (N/S/E/W) drawn with `DrawTextBillboard`, rotated by compass heading
- Sits below the debug panel and SETTINGS text

### 4. "Set North" Calibration

- New `CompassCalibrationStore` (static, thread-safe): persists offset via `ApplicationData.Current.LocalSettings`
- Settings dialog adds "Set North" option after location options
- Flow: user gazes north → taps "Set North" → records `currentHeading - 0°` as offset
- All rendering applies offset: `calibratedHeading = rawHeading - offset`

### 5. Persistence

- Store calibration offset in `Windows.Storage.ApplicationData.Current.LocalSettings.Values["northCalibrationOffset"]`
- On startup, `CompassService.Initialize()` reads saved offset

## File Changes

| File | Change |
|---|---|
| `Services/CompassService.cs` | Add `CalibrationOffset`, `CalibratedHeading`, `SaveOffset()`/`LoadOffset()` |
| `Services/CompassCalibrationStore.cs` | **NEW** — persistent offset storage via LocalSettings |
| `Content/AirplaneRenderer.cs` | Add radar disc, dotted lines, distance labels, floor compass |
| `BasicHologramMain.cs` | Add "Set North" option in settings dialog |
| `docs/radar-hologram-plan.md` | **NEW** — this document |

## Render Order (per frame)

1. Floor compass disc + arrows
2. Airplane cubes (existing)
3. Dotted lines radar→airplanes + distance labels
4. Radar hologram disc
5. SETTINGS text + debug panel (existing)

## Technical Constraints

- SharpDX 3.0.2 — no mesh libraries, only `Vector3`/`Matrix4x4` geometry
- HoloLens 1 has magnetometer (AK8963); `Compass.GetDefault()` works — calibration compensates for magnetic declination/drift
- Vertex budget is well within HoloLens 1 limits (15 airplanes × line segments + radar + compass)

## Implementation Order

1. `CompassCalibrationStore` + calibration offset in `CompassService` (backend, testable without rendering)
2. "Set North" dialog option in `BasicHologramMain`
3. Floor compass rendering in `AirplaneRenderer`
4. Radar hologram mesh + position above SETTINGS
5. Dotted lines + distance labels (most complex — depends on 4)

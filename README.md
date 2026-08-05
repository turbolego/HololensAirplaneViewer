# HololensAirplaneViewer

[![UWP Build](https://github.com/turbolego/HololensAirplaneViewer/actions/workflows/dotnet.yml/badge.svg)](https://github.com/turbolego/HololensAirplaneViewer/actions/workflows/dotnet.yml)
[![UWP Package](https://github.com/turbolego/HololensAirplaneViewer/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/turbolego/HololensAirplaneViewer/actions/workflows/dotnet-desktop.yml)
[![Store Submission](https://github.com/turbolego/HololensAirplaneViewer/actions/workflows/store-submission.yml/badge.svg)](https://github.com/turbolego/HololensAirplaneViewer/actions/workflows/store-submission.yml)
![Platform x86](https://img.shields.io/badge/platform-x86-blue)
![SDK 10.0.19041](https://img.shields.io/badge/Windows%20SDK-10.0.19041-blue)
![HoloLens 1](https://img.shields.io/badge/HoloLens-1st%20gen-blueviolet)

Real-time airplane tracker for **Microsoft HoloLens 1** built on the UWP platform.
Airplanes appear as holographic markers in the dome **above you**, positioned from live ADS-B data fetched from the [OpenSky Network](https://opensky-network.org/). A debug panel below the GPS location shows each aircraft's ICAO, callsign, altitude, and relative position.

---
## What You Will See
Put on the HoloLens and launch the app:

- Airplanes appear as small coloured cubes **above you** in a dome arrangement, their positions computed from live OpenSky state vectors and your GPS location. The closest 15 airplanes are shown.
- A **text panel** floats in front of you below eye level, listing:
  - Your current **GPS coordinates**
  - ADS-B data stats (loaded, propagated, above horizon)
  - Each visible airplane's **ICAO, callsign, altitude, and relative X/Z position**
- Because black pixels are transparent on HoloLens's see-through display, the scene appears to **float in the real world** — no background, no window frame.

---
## How It Works
| Step | Detail |
|------|--------|
| **GPS** | HoloLens `Geolocator` gets the device's current lat/lon/altitude |
| **OpenSky fetch** | Fetches live aircraft state vectors from OpenSky Network every 10 seconds (anonymous tier) |
| **GPS→Local mapping** | Converts each airplane's WGS-84 lat/lon/alt to local HoloLens coordinates using a planar approximation around the user's GPS fix |
| **Sorting** | The closest airplanes (by horizontal distance, biased airborne) are selected |
| **Rendering** | Direct3D 11 holographic pipeline draws each airplane as a colour-coded cube with a label above it, positioned 0.4 m to the ceiling (with some vertical spread per altitude) |
| **Text panel** | Custom bitmap glyph rendering via a geometry shader animates the info list in 3D at a fixed offset from the user's head position |

No external render engine — all rendering is custom Direct3D 11 with SharpDX.

---
## Project Structure
```
HololensAirplaneViewer/
├── .github/workflows/
│   ├── dotnet.yml # CI compile check (Debug + Release)
│   ├── dotnet-desktop.yml # Signed .appxartifact on push
│   └── store-submission.yml # Full Store pipeline: build, WACK, package, submit
├── Assets/ # PNG logos/splash at required sizes
├── Common/
│   └── DeviceResources.cs # Direct3D device management
├── Content/
│   ├── AirplaneRenderer.cs # Holographic airplane cube + text rendering
│   ├── SpatialInputHandler.cs # Gesture/click input
│   └── SpinningCubeRenderer.cs # Sample cube (from template)
├── Helpers/
│   └── HolographicPositioning.cs # Lat/lon/alt to world coordinates
├── Models/
│   └── AirplaneState.cs # Aircraft data model (icao24, callsign, lat/lon/alt)
├── Services/
│   ├── GeolocationService.cs # HoloLens GPS provider
│   └── AirplaneService.cs # OpenSky HTTP client
├── privacy/
│   └── index.html # Privacy policy (served via GitHub Pages)
├── Properties/
│   └── AssemblyInfo.cs
├── BasicHologramMain.cs # App lifecycle + holographic frame loop
├── HololensAirplaneViewer.csproj # UWP project — .NETCore 5.0, x86
├── HololensAirplaneViewer_TemporaryKey.pfx # Dev signing cert
├── Package.appxmanifest # Identity, capabilities, logos
└── deploy.ps1 # One-shot deploy to HoloLens over USB
```
---
## Prerequisites
| Requirement | Version / Notes |
|-------------|-----------------|
| Windows | 10 or 11 (64-bit) |
| Visual Studio 2022 | Community (free) or higher — **UWP workload** required |
| Windows 10 SDK | **10.0.19041.0** (included in UWP workload) |
| HoloLens 1 | Developer Mode enabled |
| Cable | Micro-USB to USB-A |

---
## Build
Use MSBuild from Visual Studio 2022. The cross-platform `dotnet build` CLI cannot resolve Windows XAML targets.

```powershell
$msbuild = "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe"
```

### Debug build — compile check only
```powershell
& $msbuild HololensAirplaneViewer.csproj `
  /p:Configuration=Debug `
  /p:Platform=x86 `
  /p:AppxPackageSigningEnabled=false `
  /p:GenerateAppxPackageOnBuild=false `
  /v:minimal
```

### Release build — signed .appxupload (Store-ready)
```powershell
& $msbuild HololensAirplaneViewer.csproj `
  /t:Publish `
  /p:Configuration=Release `
  /p:Platform=x86 `
  /p:AppxBundle=Never `
  /p:UapAppxPackageBuildMode=StoreUpload `
  /p:AppxPackageDir=AppPackages\ `
  /p:AppxPackageSigningEnabled=true `
  /p:PackageCertificateKeyFile=HololensAirplaneViewer_TemporaryKey.pfx `
  /p:PackageCertificatePassword=ci `
  /v:minimal
```

Output lands in `AppPackages\\HololensAirplaneViewer_1.0.0.0_x86_Test\`.

---
## Deploy to HoloLens
### 1. Enable Developer Mode on HoloLens
1. **Start menu → Settings → Update & Security → For developers**
2. Toggle **Use developer features → On**
3. Toggle **Enable Device Portal → On**

### 2. Connect via USB
Connect the HoloLens with a **Micro-USB to USB-A** cable. Windows installs a **Remote NDIS (RNDIS)** driver — the device becomes reachable at `127.0.0.1`.

### 3. Install with WinAppDeployCmd
```powershell
# Locate the tool
$wadc = (Get-ChildItem "C:\\Program Files (x86)\\Windows Kits\\10\\bin"
  -Recurse -Filter "WinAppDeployCmd.exe"
  | Sort-Object FullName | Select-Object -Last 1).FullName
# Install the .appx + dependencies
$pkg = "AppPackages\\HololensAirplaneViewer_1.0.0.0_x86_Test"
& $wadc install `
  -f "$pkg\\HololensAirplaneViewer_1.0.0.0_x86.appx" `
  -ip 127.0.0.1 `
  -d "$pkg\\Dependencies\\x86\\Microsoft.NET.Native.Framework.1.3.appx" `
  -d "$pkg\\Dependencies\\x86\\Microsoft.NET.Native.Runtime.1.4.appx" `
  -d "$pkg\\Dependencies\\x86\\Microsoft.VCLibs.x86.14.00.appx"
```

First-time pairing: the HoloLens shows a 6-digit PIN — add `-pin 123456`.

### Quick re-deploy
```powershell
powershell -ExecutionPolicy Bypass -File .\\deploy.ps1
```
---
## CI / CD
Three GitHub Actions workflows run on `windows-2022` runners.

| Workflow | Trigger | Produces |
|----------|---------|----------|
| `dotnet.yml` | Push / PR to `master` | Compile check (Debug + Release) |
| `dotnet-desktop.yml` | Push to `master` | Signed `.appxupload` artifact |
| `store-submission.yml` | Tag `v*.*.*` | `.appxupload` + WACK + optional Store publish |

### `dotnet.yml` — compile check
Builds Debug and Release with signing disabled. Catches build regressions.

### `dotnet-desktop.yml` — signed artifact
Generates a fresh self-signed cert per run, builds a full signed Release `.appxupload` via the `Publish` target with `UapAppxPackageBuildMode=StoreUpload`, produces a downloadable artifact, then removes the cert.

### `store-submission.yml` — Store pipeline
Triggered by a `v*.*.*` git tag. Same build as above plus:
1. Windows App Certification Kit (WACK) validation
2. Submission to Partner Center via `microsoft-store-apppublisher` action (requires `AZURE_AD_TENANT_ID`, `AZURE_AD_CLIENT_ID`, `AZURE_AD_CLIENT_SECRET`, `SELLER_ID` secrets configured in repo)

---
## Microsoft Store
The `.appxupload` from the CI artifact can be uploaded directly to [Partner Center](https://partner.microsoft.com/dashboard).

Supported architecture: **x86** (HoloLens 1).

Package identity: `Turbolego.HololensAirplaneViewer`
Publisher: `CN=BB1A7F2A-A87C-44C8-8C14-84C6486E7E75`

---
## Privacy Policy
This app does not collect or transmit personal information. The location, webcam, and microphone capabilities are used exclusively for HoloLens platform operation — no data leaves the device except for public state vector requests to OpenSky Network.

Full policy: https://turbolego.github.io/HololensAirplaneViewer/privacy/
# VerifyAirplanes

End-to-end verifier for the airplane HoloLens app pipeline against **live
[OpenSky Network](https://opensky-network.org/) data** — exercises the actual
production source files (linked via `<Compile Include>`), not copies.

Replicates `AirplaneService.GetLiveStatesAsync` (bbox query + OpenSky
17-field parse) and `AirplaneRenderer.Update` (airborne-first selection with
nearby ground traffic + line-of-sight filter), then prints exactly which
aircraft would be rendered in the HoloLens dome.

## Usage

```bash
dotnet run                                   # default: Gardermoen (ENGM), probe SAS1314
dotnet run -- <lat> <lon>                    # any observer GPS fix
dotnet run -- <lat> <lon> <callsign>         # also probe a specific flight
```

Example:

```bash
dotnet run -- 58.2042 8.0853 DLH    # Kristiansand airport, probe Lufthansa flights
```

## Output

- HTTP status + raw response sanity
- Parsed aircraft count in the ±3° box (pins the 17-field OpenSky contract:
  `AirplaneMath.OpenSkyStateVectorMinimumFields`)
- What survives `AirplaneSelection.Select` (top 15, airborne first, ground
  traffic < 15 km reserved) and `OnlyVisibleFrom` (line of sight at 1.5 m)
- Dome position (`DomeX/Y/Z`) for each rendered aircraft
- Target-callsign report: is it in the box, did it make the selection, is it
  rendered — through the **full** pipeline, so slot competition is real

## Notes

- Requires .NET 8 SDK.
- Live data: aircraft come and go — a flight present one minute may be gone
  the next (OpenSky only reports aircraft currently broadcasting).
- The ±3° box, top-15 cap and 15 km ground-traffic radius mirror the app's
  constants exactly.

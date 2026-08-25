# Development Notes & Hardware Constraints

## Dependency Policy: SharpDX
**Do NOT upgrade SharpDX dependencies beyond version 3.0.2.**

### Reasoning
The project targets the **Microsoft HoloLens 1st Gen**, which operates on an x86 Intel Atom CPU architecture and relies on the **.NET Native AOT compilation** pipeline.

*   **Version 3.0.2:** Verified stable and compatible with the .NET Native toolchain on the HoloLens 1 Windows Holographic OS.
*   **Version 4.x:** While functionally similar on desktop, the 4.x series (the final release branch) introduced Marshalling and Interop changes that cause severe issues during .NET Native AOT compilation for x86. Common symptoms include:
    *   `TypeLoadException` at runtime.
    *   Namespace conflicts between `SharpDX.Mathematics` and `System.Numerics`.
    *   Silent crashes in `Release` builds on the physical HoloLens 1 device.

Any attempt to upgrade `SharpDX` beyond 3.0.2 will likely break the production build. Ensure Renovate or future manual updates do not increment these packages.

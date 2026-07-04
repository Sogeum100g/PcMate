# PcMate

PcMate is a Windows desktop overlay app that visualizes PC resource state as a character status.

The current MVP focuses on memory usage:

```text
memory usage collection
-> four-state classification
-> topmost resizable WPF overlay
```

## Current Scope

- WPF desktop app
- Topmost resizable window
- Physical memory usage polling every second
- `Lying`, `Sitting`, `Walking`, `Running` state classification
- PNG frame animation using the test assets under `assets/characters`
- Window size and position restore between launches
- Basic MVVM-style separation between monitoring, classification, and UI

Future expansion ideas are tracked separately in [PcMate_EXPANSION_PLAN.md](./PcMate_EXPANSION_PLAN.md).

## Run

```powershell
dotnet run --project PcMate.csproj
```

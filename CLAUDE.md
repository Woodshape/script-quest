# ScriptQuest — Claude Guidelines

## Build & Run
- `dotnet build` to compile
- `dotnet run` to launch the game
- .NET 8 + MonoGame + MoonSharp (Lua)

## Code Style Preferences
- Prefer reusing existing methods over duplicating filter logic, even if it means double enumeration. Readability and single source of truth matter more than micro-optimization for small collections.

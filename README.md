# rpgFogOfWar

WPF fog-of-war map tool for tabletop RPGs. A DM control window paints reveal and cover, markers, and area-of-effect shapes; an audience window shows the player view.

## Requirements

- Windows
- .NET 8 SDK
- Two displays recommended (DM machine + projector/TV)

## Run

```bash
dotnet run --project rpgFogOfWar.csproj
```

## Usage

- **F** — load a map image (PNG, JPEG, BMP, or animated GIF)
- **R** — reset fog
- **Ctrl+S** / **Ctrl+O** — save / load a session (`.mapSes` plus a copied map file and a fog PNG next to it)
- Left drag — reveal or cover fog (see the sidebar tool)
- Shift + left drag — pan
- Mouse wheel — zoom; **Ctrl+wheel** always zooms
- With an AoE shape selected, left click places the shape; wheel resizes, Shift+wheel rotates
- Right click — place a condition marker (including Generic)
- Shift + right click — delete a marker
- **Ctrl+Z** — undo the last marker or shape

The audience display picker sends the player view to a second monitor (fullscreen, topmost) or keeps it as a normal window. On a single screen the default is windowed so the DM UI stays usable.

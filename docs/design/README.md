# Hollywood terminal visual references

These PNGs are generated concept images, not application screenshots. They establish visual direction. The runtime must use fixed character cells, actual tested ANSI colors, clipped text, and synthetic content.

`actual-120x30-snapshot.txt` is plain text captured from the executable's snapshot mode. It verifies cell content and grid structure, not interactive VT rendering or terminal colors.

| Reference | Intended viewport | Target composition |
|---|---:|---|
| `hollywood-compact-80x24.png` | 80×24 | Two narrow panels above a full-width rain panel, with short status/footer. |
| `hollywood-master-120x30.png` | 120×30 | Six panels in a 3×2 grid with one header row and two footer rows. |
| `hollywood-dense-160x45.png` | 160×45 | Dense multi-panel layout with larger code, metrics, log, topology, and data regions. |

The generated concept images may contain inaccurate sample values, labels, clocks, or IP addresses. The real application does not copy those values. Runtime synthetic network samples must use documentation-reserved ranges. The images are design targets only, not proof of terminal behavior.

## 120×30 cell layout target

- Row 0: application name, simulation marker, theme, visible panel count.
- Rows 1–13: code stream, fictional network traffic, digital rain.
- Rows 14–27: data processing, security activity, system metrics.
- Row 28: keyboard hints and current state.
- Row 29: synthetic-data and network-safety label.
- Column ranges are `[0,40)`, `[40,80)`, and `[80,120)` for each panel row. Each panel owns its border cells. The renderer never writes the physical lower-right terminal cell.

## Visual acceptance

- Panel rectangles align to the cell grid and never overlap.
- Every title fits its panel and every panel visibly identifies simulated data where relevant.
- Green is the primary text color. Cyan marks secondary structure. Amber is reserved for warnings.
- Text is legible at the requested viewport. Long rows are clipped and never wrap into neighboring panes.
- The actual terminal capture is compared with the concept at matching dimensions. Differences are logged before claims of visual completion.

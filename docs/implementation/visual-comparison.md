# Visual comparison status

The three PNGs in `docs/design/` are generated concept references, not application screenshots. `docs/design/actual-120x30-snapshot.txt` is a plain character-cell snapshot from an earlier build, not evidence from an interactive terminal.

The current implementation has five theme palettes and an ASCII layout. Cell-level tests check clipping, panel bounds, representative labels, and renderer diffs. A visual comparison against the generated PNGs has not been completed; those images are illustrative and cannot establish runtime rendering fidelity.

Next: capture the current renderer output at 80×24, 120×30, and 160×45; compare borders, label alignment, density, and clipping using cell-coordinate assertions; then inspect actual ConPTY/Windows Terminal captures for color and resize behavior.

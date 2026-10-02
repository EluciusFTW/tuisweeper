# Minesweeper

A single-player terminal Minesweeper built with [SpectreTuff](https://github.com/EluciusFTW/SpectreTuff)
(Spectre.Tui) and Elmish.

```sh
cd src && dotnet run
```

| Key                    | Action                                                                         |
| ---------------------- | ------------------------------------------------------------------------------ |
| ←↑↓→ / `h` `j` `k` `l` | move cursor                                                                    |
| `Space` / `Enter`      | reveal; on a number with all its mines flagged, reveals the neighbours (chord) |
| `f`                    | toggle flag                                                                    |
| `n`                    | new game                                                                       |
| `1` `2` `3`            | Beginner (9×9, 10) · Intermediate (16×16, 40) · Expert (30×16, 99)             |
| `q`                    | quit                                                                           |

The first reveal is always safe: mines are placed after it, away from the clicked cell.
Goal is to reveal all safe squares - mines do not have to be flagged.

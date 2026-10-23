# tuigames

Single-player terminal games built with [SpectreTuff](https://github.com/EluciusFTW/SpectreTuff)
(Spectre.Tui) and Elmish. Play them from one launcher, or embed any game as a component in your own Elmish
application via its NuGet package (see [Project structure](#project-structure)).

| Game           | What it is                       | Package                                                                      |
| -------------- | -------------------------------- | ---------------------------------------------------------------------------- |
| **tuisweeper** | Minesweeper                      | [`EluciusFTW.Tuisweeper`](https://www.nuget.org/packages/EluciusFTW.Tuisweeper) |
| **tui48**      | 2048-style sliding tile game     | [`EluciusFTW.Tui48`](https://www.nuget.org/packages/EluciusFTW.Tui48)           |

![tuisweeper: a lost game on the Intermediate board](assets/intermediate.png)

## Running / Installing

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). You can run directly from source:

```sh
dotnet run --project src/Tuigames.Cli          # start on the game menu
dotnet run --project src/Tuigames.Cli -- tui48 # or go straight to a game
```

Alternatively, use the included scripts to build a
single-file, framework-dependent executable and copy it onto your `PATH` as `tuigames`.

**Linux / macOS:**

```bash
./scripts/install.sh             # installs to ~/.local/bin (or $TUIGAMES_INSTALL_DIR)
./scripts/install.sh /custom/dir # or a custom directory
```

**Windows (PowerShell):**

```powershell
scripts\install.ps1                      # installs to %LOCALAPPDATA%\Programs\tuigames
scripts\install.ps1 -InstallDir C:\tools # or a custom directory
```

If the chosen directory is not already on your `PATH`, the script prints how to add it.

`tuigames` opens a menu: pick a game with ↑↓ and `Enter`, or its number. `q` / `Esc` in a game returns to the
menu, and in the menu quits. `tuigames <game>` (e.g. `tuigames tuisweeper`) skips the menu.

### tuisweeper

| Key                    | Action                                                                         |
| ---------------------- | ------------------------------------------------------------------------------ |
| ←↑↓→ / `h` `j` `k` `l` | move cursor                                                                    |
| `Space` / `Enter`      | reveal; on a number with all its mines flagged, reveals the neighbours (chord) |
| `f`                    | toggle flag                                                                    |
| `n`                    | new game                                                                       |
| `1` `2` `3`            | Beginner (9×9, 10) · Intermediate (16×16, 40) · Expert (30×16, 99)             |
| `q` / `Esc`            | back to the menu                                                               |

The first reveal is always safe: mines are placed after it, away from the clicked cell.
Goal is to reveal all safe squares - mines do not have to be flagged.

Your best time per difficulty is shown while playing and saved to `~/.config/tuisweeper/highscores.txt`
(`%APPDATA%\tuisweeper\highscores.txt` on Windows).

### tui48

A terminal take on [2048](https://github.com/gabrielecirulli/2048) by Gabriele Cirulli: slide the tiles, merge
equal ones, and reach the 2048 tile.

| Key                    | Action                         |
| ---------------------- | ------------------------------ |
| ←↑↓→ / `h` `j` `k` `l` | slide the tiles                |
| `c`                    | keep going after reaching 2048 |
| `n`                    | new game                       |
| `3` `4` `5` `6`        | new game on a 3x3 to 6x6 board |
| `q` / `Esc`            | back to the menu               |

The best score per board size is saved to `~/.config/tui48/highscores.txt` (`%APPDATA%\tui48\highscores.txt` on
Windows) when a game ends, is replaced by a new one, or the game is closed.

## Project structure

Each game is a library, so it can be embedded in other SpectreTuff.Elmish apps (e.g. as a pop-up game in
[tuigether](https://github.com/EluciusFTW/tuigether)); one thin executable hosts them all:

| Project                | Output                                                       | Contents                                                                                                   |
| ---------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------- |
| `src/Tuisweeper.Core/` | `Tuisweeper.Core.dll`, NuGet package `EluciusFTW.Tuisweeper` | Minesweeper as an Elmish component (`init`, `update`, `view`, `subscriptions`), board logic, highscore stores |
| `src/Tui48.Core/`      | `Tui48.Core.dll`, NuGet package `EluciusFTW.Tui48`           | The 2048-style game as an Elmish component, board logic, highscore stores                                  |
| `src/Tuigames.Cli/`    | the `tuigames` executable                                    | The menu, and hosting of each game component full-screen                                                   |

Every game library exposes the same shape: a module named after the game (`Tuisweeper`, `Tui48`) with `Model`,
`Msg`, `init`, `update`, `view`, `subscriptions` and `keyMap`, plus a `Highscore.Store`; everything else is
internal. A component never ends the program or chooses where data lives on its own:

- **Dependencies** (`<Game>.Dependencies`): the highscore store and random source (and clock, where needed) are
  passed in by the host. `Highscore.Store` provides `defaultFile` (the paths above), `file path` and `none`, or
  bring your own `{ Load; Save }`.
- **Closing**: `update` returns `Some Closed` when the player presses `q`/`Esc`; the launcher goes back to its
  menu, an embedding app might hide an overlay instead. Keep the command returned with it.
- **Keys**: the host owns the terminal and routes key presses in as `<Game>.InputMsg`.

`src/Tuigames.Cli/Program.fs` is a worked example of hosting several games; each package README has a minimal one.

To add a game: create `src/<Game>.Core/` following the same shape, add a case for it to `Menu.Game` and
`Menu.entries` in `src/Tuigames.Cli/Menu.fs` and to the `Screen` and `Msg` types in `Program.fs`, and copy one of
the publish workflows.

### Publishing

Each library is published to NuGet by its own workflow when changes under its folder are pushed to `main`:
[`tuisweeper-publish.yml`](.github/workflows/tuisweeper-publish.yml) for `src/Tuisweeper.Core/` (and the root
`version.json`), [`tui48-publish.yml`](.github/workflows/tui48-publish.yml) for `src/Tui48.Core/`. The executable
is not packed. Versions come from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning): a
`version.json` sets major.minor (the root one for tuisweeper, `src/Tui48.Core/version.json` for tui48), the commit
count the patch. To try a package locally before publishing:

```sh
dotnet pack src/Tui48.Core -c Release -o ~/nuget-local
```

## License

[MIT](LICENSE.md)

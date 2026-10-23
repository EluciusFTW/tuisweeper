# EluciusFTW.Tui48

A 2048-style sliding tile game as an [Elmish](https://elmish.github.io/elmish/) component for
[SpectreTuff.Elmish](https://github.com/EluciusFTW/SpectreTuff) programs. Host it as a whole app,
or embed it in your own, for example as a pop-up game.

Slide the tiles with the arrow keys (or `h` `j` `k` `l`); equal tiles merge and add to your score.
Reach the 2048 tile to win, then keep going if you like. Boards from 3x3 to 6x6 (keys `3`–`6`), with
the best score per board size kept.

Based on the original [2048](https://github.com/gabrielecirulli/2048) by Gabriele Cirulli.

## Hosting the component

```fsharp
open Tui48

// Highscores: Store.defaultFile (shared with the standalone game), Store.file path,
// Store.none, or your own { Load = ...; Save = ... }.
let deps = Tui48.Dependencies.create Highscore.Store.defaultFile

let init () = Tui48.init deps

let update msg model =
  let model, cmd, out = Tui48.update deps msg model

  match out with
  | Some Tui48.Closed -> model, Cmd.batch [ cmd; TuiCmd.exit ] // the player pressed q or Esc
  | None -> model, cmd

Elmish.Program.mkProgram init update Tui48.view
|> Elmish.Program.withSubscription (fun model -> Subscriptions.keys Tui48.InputMsg @ Tui48.subscriptions model)
|> Tui.run
```

When embedding, wrap the component's messages in your own (`TileMsg of Tui48.Msg`), route key
presses to it as `Tui48.InputMsg key`, render `Tui48.view` on top of your view, and decide
what `Closed` means, e.g. hiding the overlay instead of exiting. Keep the `cmd` returned along with
`Closed`: it saves a new highscore of the game in progress.

See `src/Tuigames.Cli` in this repository for a host that embeds this and other games behind a menu.

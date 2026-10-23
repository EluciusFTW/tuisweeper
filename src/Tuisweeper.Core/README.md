# EluciusFTW.Tuisweeper

Minesweeper as an [Elmish](https://elmish.github.io/elmish/) component for
[SpectreTuff.Elmish](https://github.com/EluciusFTW/SpectreTuff) programs. Host it as a whole app,
or embed it in your own, for example as a pop-up game.

## Hosting the component

```fsharp
open Tuisweeper

// Highscores: Store.defaultFile (shared with the standalone game), Store.file path,
// Store.none, or your own { Load = ...; Save = ... }.
let deps = Tuisweeper.Dependencies.create Highscore.Store.defaultFile

let init () = Tuisweeper.init deps

let update msg model =
  let model, cmd, out = Tuisweeper.update deps msg model

  match out with
  | Some Tuisweeper.Closed -> model, Cmd.batch [ cmd; TuiCmd.exit ] // the player pressed q or Esc
  | None -> model, cmd

Elmish.Program.mkProgram init update Tuisweeper.view
|> Elmish.Program.withSubscription (fun model ->
  Subscriptions.keys Tuisweeper.InputMsg @ Tuisweeper.subscriptions model)
|> Tui.run
```

When embedding, wrap the component's messages in your own (`SweeperMsg of Tuisweeper.Msg`), route key
presses to it as `Tuisweeper.InputMsg key`, render `Tuisweeper.view` on top of your view, and decide
what `Closed` means, e.g. hiding the overlay instead of exiting.

See the CLI project in this repository for a minimla example.

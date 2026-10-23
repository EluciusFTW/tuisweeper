open Elmish
open Tuisweeper
open SpectreTuff.Elmish

let private deps = Tuisweeper.Dependencies.create Highscore.Store.defaultFile

let update msg model =
  let model, cmd, out = Tuisweeper.update deps msg model

  match out with
  | Some Tuisweeper.Closed -> model, Cmd.batch [ cmd; TuiCmd.exit ]
  | None -> model, cmd

Elmish.Program.mkProgram (fun () -> Tuisweeper.init deps) update Tuisweeper.view
|> Elmish.Program.withSubscription (fun model ->
  Subscriptions.keys Tuisweeper.InputMsg @ Tuisweeper.subscriptions model)
|> Tui.run

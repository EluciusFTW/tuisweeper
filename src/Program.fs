open System
open SpectreTuff.Elmish

Elmish.Program.mkProgram Application.init Application.update AppView.view
|> Elmish.Program.withSubscription (fun model ->
  Subscriptions.keys Application.InputMsg
  @ (match model.Board.Status with
     | Game.Playing -> Subscriptions.tick (TimeSpan.FromSeconds 1.0) Application.Tick
     | _ -> []))
|> Tui.run

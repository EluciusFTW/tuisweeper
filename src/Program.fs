open System

let terminal = Spectre.Tui.Terminal.Create()
// Work around ConPTY alt-screen sizing bug: exit and re-enter alt-screen
// so ConPTY allocates the buffer with the real window dimensions.
Console.Write "\x1b[?1049l"
Console.Out.Flush()
System.Threading.Thread.Sleep 30
Console.Write "\x1b[?1049h"
Console.Out.Flush()

let renderer = Spectre.Tui.Renderer terminal
renderer.NoTargetFps()

Elmish.Program.mkProgram Application.init Application.update (AppView.view renderer)
|> Elmish.Program.withSubscription (fun model ->
  Input.subscription Application.InputMsg model
  @ Tick.subscription (TimeSpan.FromMilliseconds 250.0) Application.Tick model)
|> Elmish.Program.run

Application.exitEvent.Wait()
terminal.Dispose()
Console.Clear()

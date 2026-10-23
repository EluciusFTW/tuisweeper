open System
open Elmish
open SpectreTuff.Elmish
open Tuigames
open Tuisweeper
open Tui48

// Hosts every game: starts on the menu (or directly in the game named as the argument),
// and a game's Closed brings the player back to the menu.

type Screen =
  | Menu
  | Sweeper of Tuisweeper.Model
  | Tile of Tui48.Model

type Model = {
  /// The menu entry under the cursor; kept while playing, so the menu reopens on the last game.
  Selected: int
  Screen: Screen
}

type Msg =
  | KeyPressed of ConsoleKeyInfo
  | MenuMsg of Menu.Msg
  | SweeperMsg of Tuisweeper.Msg
  | TileMsg of Tui48.Msg

let private sweeperDeps = Tuisweeper.Dependencies.create Tuisweeper.Highscore.Store.defaultFile

let private tileDeps = Tui48.Dependencies.create Tui48.Highscore.Store.defaultFile

let private start (game: Menu.Game) (model: Model) =
  let selected = Menu.entries |> List.findIndex (fun e -> e.Game = game)

  match game with
  | Menu.Sweeper ->
    let sweeper, cmd = Tuisweeper.init sweeperDeps

    {
      Selected = selected
      Screen = Sweeper sweeper
    },
    Cmd.map SweeperMsg cmd
  | Menu.Tile ->
    let tile, cmd = Tui48.init tileDeps

    {
      Selected = selected
      Screen = Tile tile
    },
    Cmd.map TileMsg cmd

let init (game: Menu.Game option) () =
  let model = { Selected = 0; Screen = Menu }

  match game with
  | Some game -> start game model
  | None -> model, Cmd.none

// Shows the game, or the menu again once the game reports it was closed.
let private hosted screen wrap (model: Model) (game, cmd, out) =
  let screen =
    match out with
    | Some() -> Menu
    | None -> screen game

  { model with Screen = screen }, Cmd.map wrap cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  match msg, model.Screen with
  | KeyPressed key, Menu ->
    match Menu.handleKey key model.Selected with
    | Some m -> model, Cmd.ofMsg (MenuMsg m)
    | None -> model, Cmd.none
  | KeyPressed key, Sweeper _ -> model, Cmd.ofMsg (SweeperMsg(Tuisweeper.InputMsg key))
  | KeyPressed key, Tile _ -> model, Cmd.ofMsg (TileMsg(Tui48.InputMsg key))

  | MenuMsg(Menu.Move delta), Menu ->
    {
      model with
          Selected = Menu.move delta model.Selected
    },
    Cmd.none
  | MenuMsg(Menu.Start game), Menu -> start game model
  | MenuMsg Menu.Quit, Menu -> model, TuiCmd.exit

  | SweeperMsg m, Sweeper sweeper ->
    let sweeper, cmd, out = Tuisweeper.update sweeperDeps m sweeper
    hosted Sweeper SweeperMsg model (sweeper, cmd, out |> Option.map ignore)
  | TileMsg m, Tile tile ->
    let tile, cmd, out = Tui48.update tileDeps m tile
    hosted Tile TileMsg model (tile, cmd, out |> Option.map ignore)

  // Leftovers for a screen that is no longer shown, e.g. a timer tick after closing a game.
  | _ -> model, Cmd.none

let subscriptions (model: Model) : Sub<Msg> =
  let game =
    match model.Screen with
    | Menu -> []
    | Sweeper sweeper -> Tuisweeper.subscriptions sweeper |> Sub.map "sweeper" SweeperMsg
    | Tile tile -> Tui48.subscriptions tile |> Sub.map "tile" TileMsg

  Subscriptions.keys KeyPressed @ game

let view (model: Model) (dispatch: Msg -> unit) =
  match model.Screen with
  | Menu -> Menu.view model.Selected
  | Sweeper sweeper -> Tuisweeper.view sweeper (SweeperMsg >> dispatch)
  | Tile tile -> Tui48.view tile (TileMsg >> dispatch)

let private usage () =
  let names = Menu.entries |> List.map _.Name |> String.concat " | "
  eprintfn "usage: tuigames [%s]" names

match Environment.GetCommandLineArgs() |> Array.tail with
| [||] -> Some None
| [| name |] -> Menu.tryFind name |> Option.map (fun e -> Some e.Game)
| _ -> None
|> function
  | Some game ->
    Elmish.Program.mkProgram (init game) update view
    |> Elmish.Program.withSubscription subscriptions
    |> Tui.run
  | None ->
    usage ()
    exit 1

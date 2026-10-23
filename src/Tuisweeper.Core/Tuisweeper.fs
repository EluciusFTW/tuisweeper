module Tuisweeper.Tuisweeper

open System
open Elmish
open Spectre.Console
open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff
open SpectreTuff.Elmish
open SpectreTuff.Layout
open SpectreTuff.Widgets
open Game
open Render

type Model = internal {
  Board: Board
  Difficulty: Difficulty
  Cursor: int * int
  StartedAt: DateTime option
  Elapsed: TimeSpan
  Highscores: Highscore.Highscores
  NewRecord: TimeSpan option option
}

type Msg =
  | InputMsg of ConsoleKeyInfo
  | Move of dx: int * dy: int
  | Reveal
  | ToggleFlag
  | NewGame of Difficulty
  | Tick
  | Close

type OutMsg = | Closed

type Dependencies = {
  Store: Highscore.Store
  Random: Random
  Now: unit -> DateTime
}

module Dependencies =
  let create (store: Highscore.Store) = {
    Store = store
    Random = Random()
    Now = fun () -> DateTime.UtcNow
  }

let private newGame highscores difficulty = {
  Board = Game.create difficulty
  Difficulty = difficulty
  Cursor = 0, 0
  StartedAt = None
  Elapsed = TimeSpan.Zero
  Highscores = highscores
  NewRecord = None
}

let private groupedBindings: Keymap.KeyBinding<Model, Msg> list = [
  Keymap.KeyBinding.createSpecial ConsoleKey.UpArrow "up" (Move(0, -1))
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'k')
  Keymap.KeyBinding.createSpecial ConsoleKey.DownArrow "down" (Move(0, 1))
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'j')
  Keymap.KeyBinding.createSpecial ConsoleKey.LeftArrow "left" (Move(-1, 0))
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'h')
  Keymap.KeyBinding.createSpecial ConsoleKey.RightArrow "right" (Move(1, 0))
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'l')
  Keymap.KeyBinding.create '1' "beginner" (NewGame Beginner)
  Keymap.KeyBinding.create '2' "intermediate" (NewGame Intermediate)
  Keymap.KeyBinding.create '3' "expert" (NewGame Expert)
]

let private groupedHelp = [
  [ Key.Up; Key.Down; Key.Left; Key.Right ] |> List.map KeyPress.For, "move"
  [ '1'; '2'; '3' ] |> List.map KeyPress.For, "difficulty"
]

let private bindings: Keymap.KeyBinding<Model, Msg> list = [
  Keymap.KeyBinding.dynamic (Keymap.SpecialKey ConsoleKey.Spacebar) (fun model -> {
    Description = "reveal"
    Message =
      match Game.isOver model.Board with
      | true -> None
      | false -> Some Reveal
  })
  |> Keymap.KeyBinding.orKey (Keymap.SpecialKey ConsoleKey.Enter)
  Keymap.KeyBinding.dynamic (Keymap.CharKey 'f') (fun model -> {
    Description = "flag"
    Message =
      match Game.isOver model.Board with
      | true -> None
      | false -> Some ToggleFlag
  })
  Keymap.KeyBinding.dynamic (Keymap.CharKey 'n') (fun model -> {
    Description = "new game"
    Message = Some(NewGame model.Difficulty)
  })
  Keymap.KeyBinding.create 'q' "close" Close
  |> Keymap.KeyBinding.orKey (Keymap.SpecialKey ConsoleKey.Escape)
]

let keyMap (model: Model) : IKeyMap = {
  new IKeyMap with
    member _.Help() =
      seq {
        for keys, description in groupedHelp -> KeyBinding(Keys = ResizeArray keys, Help = description)
        yield! (Keymap.KeyBinding.toKeyMap bindings model).Help()
      }
}

let init (deps: Dependencies) : Model * Cmd<Msg> =
  newGame (deps.Store.Load()) Beginner, Cmd.none

let private elapsedSince (now: DateTime) (startedAt: DateTime option) =
  startedAt |> Option.map (fun t -> now - t) |> Option.defaultValue TimeSpan.Zero

let update (deps: Dependencies) (msg: Msg) (model: Model) : Model * Cmd<Msg> * OutMsg option =
  match msg with
  | InputMsg key ->
    match Keymap.KeyBinding.handleKey (groupedBindings @ bindings) key model with
    | Some m -> model, Cmd.ofMsg m, None
    | None -> model, Cmd.none, None

  | Move(dx, dy) ->
    let x, y = model.Cursor

    let clamp hi v =
      max 0 (min (hi - 1) v)

    {
      model with
          Cursor = clamp model.Board.Width (x + dx), clamp model.Board.Height (y + dy)
    },
    Cmd.none,
    None

  | Reveal ->
    let board = Game.reveal deps.Random model.Cursor model.Board
    let now = deps.Now()

    let startedAt =
      match model.StartedAt, board.Status with
      | None, Playing -> Some now
      | started, _ -> started

    let elapsed =
      match Game.isOver board with
      | true -> elapsedSince now startedAt
      | false -> model.Elapsed

    let model = {
      model with
          Board = board
          StartedAt = startedAt
          Elapsed = elapsed
    }

    match board.Status with
    | Won when Highscore.isRecord model.Highscores model.Difficulty elapsed ->
      let highscores = model.Highscores |> Map.add model.Difficulty elapsed

      {
        model with
            Highscores = highscores
            NewRecord = Some(Map.tryFind model.Difficulty model.Highscores)
      },
      Cmd.ofEffect (fun _ -> deps.Store.Save highscores),
      None
    | _ -> model, Cmd.none, None

  | ToggleFlag ->
    {
      model with
          Board = Game.toggleFlag model.Cursor model.Board
    },
    Cmd.none,
    None

  | NewGame difficulty -> newGame model.Highscores difficulty, Cmd.none, None

  | Tick ->
    match model.Board.Status with
    | Playing ->
      {
        model with
            Elapsed = elapsedSince (deps.Now()) model.StartedAt
      },
      Cmd.none,
      None
    | Ready
    | Won
    | Lost _ -> model, Cmd.none, None

  | Close -> model, Cmd.none, Some Closed

let subscriptions (model: Model) : Sub<Msg> =
  match model.Board.Status with
  | Playing -> Subscriptions.tick (TimeSpan.FromSeconds 1.0) Tick
  | Ready
  | Won
  | Lost _ -> []

let private mainLayout =
  layout "main"
  |> splitHorizontally [| layout "content"; layout "help" |> withFixedSize (Some 1) |]

let private statusLines (model: Model) =
  let board = model.Board
  let minesLeft = board.MineCount - Game.flagCount board
  let seconds = min 999 (int model.Elapsed.TotalSeconds)

  let best =
    Map.tryFind model.Difficulty model.Highscores
    |> Option.map Highscore.format
    |> Option.defaultValue "---"

  let time = Highscore.format model.Elapsed

  // Always two message lines, so the header keeps its height across game states.
  let face, faceColor, messages =
    match board.Status, model.NewRecord with
    | Ready, _ -> ":)", Color.Yellow, [ "reveal any cell to start"; "" ]
    | Playing, _ -> ":)", Color.Yellow, [ ""; "" ]
    | Won, Some previous ->
      let previous =
        match previous with
        | Some t -> sprintf "was %s" (Highscore.format t)
        | None -> "first win"

      "B)", recordColor, [ sprintf "★ new highscore: %s ★" time; sprintf "%s · n: new game" previous ]
    | Won, None ->
      let behind =
        Map.tryFind model.Difficulty model.Highscores
        |> Option.map (fun b -> sprintf " (+%s)" (Highscore.format (model.Elapsed - b)))
        |> Option.defaultValue ""

      "B)", Color.Green3, [ sprintf "cleared in %s%s" time behind; "n: new game" ]
    | Lost _, _ -> ":(", Color.Red1, [ sprintf "boom! after %s" time; "n: try again" ]

  let messageStyle =
    match model.NewRecord with
    | Some _ -> recordStyle
    | None -> Nullable(Style faceColor)

  let counters = [
    sprintf " %03d " minesLeft, Nullable(Style Color.Red1)
    " ", Nullable()
    face, Nullable(Style faceColor)
    " ", Nullable()
    sprintf " %03d " seconds, Nullable(Style Color.Aqua)
    sprintf "  best %s" best, Nullable(Style recordColor)
  ]

  counters :: [ for m in messages -> [ sprintf " %s" m, messageStyle ] ]

let private boardWidget (model: Model) : IWidget =
  let board = model.Board

  let rows = [
    for y in 0 .. board.Height - 1 -> Text.line [ for x in 0 .. board.Width - 1 -> cellSpan board model.Cursor (x, y) ]
  ]

  let status = statusLines model
  let lines = (status |> List.map toLine) @ rows

  let borderColor =
    match board.Status, model.NewRecord with
    | Won, Some _ -> recordColor
    | Won, None -> Color.Green3
    | Lost _, _ -> Color.Red1
    | Ready, _
    | Playing, _ -> Color.Aqua

  let title = sprintf "tuisweeper · %s" (Difficulty.name model.Difficulty)

  // Three columns per cell, the status lines, plus the box border.
  let width =
    (board.Width * 3) :: (title.Length + 4) :: (status |> List.map lineWidth)
    |> List.max
    |> (+) 2

  let height = board.Height + status.Length + 2

  {
    new IWidget with
      member _.Render(ctx: RenderContext) =
        let boxed =
          box (Look.fromColor borderColor)
          |> withTitle title
          |> withInnerWidget (paragraph lines :> IWidget)
          :> IWidget

        ctx.Render(
          popup (min ctx.Viewport.Width width) (min ctx.Viewport.Height height)
          |> withPopupContent boxed
          :> IWidget
        )
  }

type private View(model: Model) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let port = getPort ctx.Viewport mainLayout
      ctx.Render(boardWidget model, port "content")
      ctx.Render(help [ keyMap model ] |> leftAligned, port "help")

let view model (_dispatch: Msg -> unit) : IWidget =
  View model :> IWidget

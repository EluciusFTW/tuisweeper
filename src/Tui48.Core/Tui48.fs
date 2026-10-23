module Tui48.Tui48

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
  Highscores: Highscore.Highscores
  /// The record for this board size when the game started, to tell whether it was beaten.
  PreviousBest: int option
  LastGain: int
}

type Msg =
  | InputMsg of ConsoleKeyInfo
  | Slide of SlideDirection
  | KeepGoing
  | NewGame of size: int
  | Close

type OutMsg = | Closed

type Dependencies = {
  Store: Highscore.Store
  Random: Random
}

module Dependencies =
  let create (store: Highscore.Store) = { Store = store; Random = Random() }

let private newGame (deps: Dependencies) highscores size = {
  Board = Game.create deps.Random size
  Highscores = highscores
  PreviousBest = Map.tryFind size highscores
  LastGain = 0
}

let private groupedBindings: Keymap.KeyBinding<Model, Msg> list = [
  Keymap.KeyBinding.createSpecial ConsoleKey.UpArrow "up" (Slide Up)
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'k')
  Keymap.KeyBinding.createSpecial ConsoleKey.DownArrow "down" (Slide Down)
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'j')
  Keymap.KeyBinding.createSpecial ConsoleKey.LeftArrow "left" (Slide Left)
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'h')
  Keymap.KeyBinding.createSpecial ConsoleKey.RightArrow "right" (Slide Right)
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'l')
]

let private sizeBindings: Keymap.KeyBinding<Model, Msg> list = [
  for size in Size.all -> Keymap.KeyBinding.create (char (int '0' + size)) (Size.name size) (NewGame size)
]

let private groupedHelp = [
  [ Key.Up; Key.Down; Key.Left; Key.Right ] |> List.map KeyPress.For, "slide"
  [ for size in Size.all -> KeyPress.For(char (int '0' + size)) ], "board size"
]

let private bindings: Keymap.KeyBinding<Model, Msg> list = [
  Keymap.KeyBinding.dynamic (Keymap.CharKey 'c') (fun model -> {
    Description = "keep going"
    Message =
      match model.Board.Status with
      | Won -> Some KeepGoing
      | Playing
      | Lost -> None
  })
  Keymap.KeyBinding.dynamic (Keymap.CharKey 'n') (fun model -> {
    Description = "new game"
    Message = Some(NewGame model.Board.Size)
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
  newGame deps (deps.Store.Load()) 4, Cmd.none

// Stores the current score if it beats the record. Called whenever a game ends: lost,
// abandoned for a new one, or closed - so a run that is still going is never lost.
let private commitScore (deps: Dependencies) (model: Model) =
  match Highscore.isRecord model.Highscores model.Board.Size model.Board.Score with
  | true ->
    let highscores = model.Highscores |> Map.add model.Board.Size model.Board.Score
    { model with Highscores = highscores }, Cmd.ofEffect (fun _ -> deps.Store.Save highscores)
  | false -> model, Cmd.none

let private withBoard (deps: Dependencies) (board: Board) (model: Model) =
  let model = { model with Board = board }

  match board.Status with
  | Lost -> commitScore deps model
  | Playing
  | Won -> model, Cmd.none

let update (deps: Dependencies) (msg: Msg) (model: Model) : Model * Cmd<Msg> * OutMsg option =
  match msg with
  | InputMsg key ->
    match Keymap.KeyBinding.handleKey (groupedBindings @ sizeBindings @ bindings) key model with
    | Some m -> model, Cmd.ofMsg m, None
    | None -> model, Cmd.none, None

  | Slide direction ->
    let board = Game.slide deps.Random direction model.Board

    let model, cmd =
      withBoard deps board {
        model with
            LastGain = board.Score - model.Board.Score
      }

    model, cmd, None

  | KeepGoing ->
    let model, cmd = withBoard deps (Game.keepGoing model.Board) model
    model, cmd, None

  | NewGame size ->
    let model, cmd = commitScore deps model
    newGame deps model.Highscores size, cmd, None

  | Close ->
    let model, cmd = commitScore deps model
    model, cmd, Some Closed

/// No timers in this game; here so hosts wire every game component alike.
let subscriptions (_model: Model) : Sub<Msg> = []

let private mainLayout =
  layout "main"
  |> splitHorizontally [| layout "content"; layout "help" |> withFixedSize (Some 1) |]

let private isNewRecord (model: Model) =
  model.Board.Score > Option.defaultValue 0 model.PreviousBest

let private statusLines (model: Model) =
  let board = model.Board
  let best = max board.Score (Highscore.best model.Highscores board.Size)

  let gain =
    match model.LastGain with
    | 0 -> ""
    | gain -> sprintf " +%d" gain

  let message, messageStyle =
    match board.Status, isNewRecord model with
    | Playing, _ when board.Score = 0 -> sprintf "join equal tiles to reach %d" goal, Nullable(Style Color.Aqua)
    | Playing, _ -> "", Nullable()
    | Won, _ -> sprintf "★ %d! ★  c: keep going · n: new game" goal, recordStyle
    | Lost, true ->
      let previous =
        match model.PreviousBest with
        | Some score -> sprintf "was %d" score
        | None -> "first game"

      sprintf "★ new highscore ★ (%s) · n: new game" previous, recordStyle
    | Lost, false -> "no moves left · n: try again", Nullable(Style Color.Red1)

  let bestStyle =
    match isNewRecord model with
    | true -> recordStyle
    | false -> Nullable(Style recordColor)

  [
    [
      sprintf " score %d" board.Score, Nullable(Style(Nullable Color.White, Nullable(), Nullable Decoration.Bold))
      gain, Nullable(Style Color.Green3)
      sprintf "   best %d" best, bestStyle
    ]
    [ sprintf " %s" message, messageStyle ]
  ]

let private boardWidget (model: Model) : IWidget =
  let board = model.Board
  let status = statusLines model

  let borderColor =
    match board.Status, isNewRecord model with
    | Won, _
    | Lost, true -> recordColor
    | Lost, false -> Color.Red1
    | Playing, _ -> Color.Aqua

  let title = sprintf "tui48 · %s" (Size.name board.Size)

  // The status lines, a blank line, then the grid centred below them.
  let content = {
    new IWidget with
      member _.Render(ctx: RenderContext) =
        ctx.Render(paragraph (status |> List.map toLine), Rectangle(0, 0, ctx.Viewport.Width, status.Length))
        let x = max 0 ((ctx.Viewport.Width - gridWidth board.Size) / 2)

        ctx.Render(gridWidget board, Rectangle(x, status.Length + 1, gridWidth board.Size, gridHeight board.Size))
  }

  // The widest of grid (with a column of padding each side), title and status lines, plus the box border.
  let width =
    (gridWidth board.Size + 2)
    :: (title.Length + 4)
    :: (status |> List.map lineWidth)
    |> List.max
    |> (+) 2

  let height = status.Length + 1 + gridHeight board.Size + 3

  {
    new IWidget with
      member _.Render(ctx: RenderContext) =
        let boxed = box (Look.fromColor borderColor) |> withTitle title |> withInnerWidget content :> IWidget

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

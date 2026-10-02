module Application

open System
open Elmish
open Spectre.Tui
open Spectre.Tui.App
open Game

type Model = {
  Board: Board
  Difficulty: Difficulty
  Cursor: int * int
  StartedAt: DateTime option
  Elapsed: TimeSpan
}

type Msg =
  | InputMsg of Input.Msg
  | Move of dx: int * dy: int
  | Reveal
  | ToggleFlag
  | NewGame of Difficulty
  | Tick
  | Exit

let exitEvent = new Threading.ManualResetEventSlim false

let private rng = Random()

let private newGame (difficulty: Difficulty) = {
  Board = Game.create difficulty
  Difficulty = difficulty
  Cursor = 0, 0
  StartedAt = None
  Elapsed = TimeSpan.Zero
}

// As in tuigether, a key's behavior and its help-bar entry come from one binding
// list, so the two can never drift apart.
// Movement and difficulty keys each get one combined help entry (see keyMap)
// instead of an entry per key, so the help bar fits a normal terminal.
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
  Keymap.KeyBinding.create 'q' "quit" Exit
]

let keyMap (model: Model) : IKeyMap =
  { new IKeyMap with
      member _.Help() =
        seq {
          for keys, description in groupedHelp -> KeyBinding(Keys = ResizeArray keys, Help = description)
          yield! (Keymap.KeyBinding.toKeyMap bindings model).Help()
        }
  }

let init () = newGame Beginner, Cmd.none

let private elapsedSince (startedAt: DateTime option) =
  startedAt |> Option.map (fun t -> DateTime.UtcNow - t) |> Option.defaultValue TimeSpan.Zero

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
  match msg with
  | InputMsg(Input.KeyPressed key) ->
    match Keymap.KeyBinding.handleKey (groupedBindings @ bindings) key model with
    | Some m -> model, Cmd.ofMsg m
    | None -> model, Cmd.none

  | Move(dx, dy) ->
    let x, y = model.Cursor
    let clamp hi v = max 0 (min (hi - 1) v)

    {
      model with
          Cursor = clamp model.Board.Width (x + dx), clamp model.Board.Height (y + dy)
    },
    Cmd.none

  | Reveal ->
    let board = Game.reveal rng model.Cursor model.Board

    let startedAt =
      match model.StartedAt, board.Status with
      | None, Playing -> Some DateTime.UtcNow
      | started, _ -> started

    // Freeze the clock the moment the game ends.
    let elapsed =
      match Game.isOver board with
      | true -> elapsedSince startedAt
      | false -> model.Elapsed

    {
      model with
          Board = board
          StartedAt = startedAt
          Elapsed = elapsed
    },
    Cmd.none

  | ToggleFlag ->
    {
      model with
          Board = Game.toggleFlag model.Cursor model.Board
    },
    Cmd.none

  | NewGame difficulty -> newGame difficulty, Cmd.none

  | Tick ->
    match model.Board.Status with
    | Playing -> { model with Elapsed = elapsedSince model.StartedAt }, Cmd.none
    | Ready
    | Won
    | Lost _ -> model, Cmd.none

  | Exit ->
    exitEvent.Set()
    model, Cmd.none

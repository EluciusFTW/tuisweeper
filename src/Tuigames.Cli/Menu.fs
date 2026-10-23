/// The startup screen: pick a game.
module Tuigames.Menu

open System
open Spectre.Console
open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Game =
  | Sweeper
  | Tile

type Entry = {
  Game: Game
  /// Also the command-line argument that starts the game directly.
  Name: string
  Description: string
}

let entries = [
  {
    Game = Sweeper
    Name = "tuisweeper"
    Description = "Minesweeper"
  }
  {
    Game = Tile
    Name = "tui48"
    Description = "2048-style sliding tiles"
  }
]

let tryFind (name: string) =
  entries |> List.tryFind (fun e -> e.Name = name)

type Msg =
  | Move of int
  | Start of Game
  | Quit

let private shortcut (index: int) =
  char (int '1' + index)

let private groupedBindings: Keymap.KeyBinding<int, Msg> list = [
  Keymap.KeyBinding.createSpecial ConsoleKey.UpArrow "up" (Move -1)
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'k')
  Keymap.KeyBinding.createSpecial ConsoleKey.DownArrow "down" (Move 1)
  |> Keymap.KeyBinding.orKey (Keymap.CharKey 'j')
]

let private shortcutBindings: Keymap.KeyBinding<int, Msg> list = [
  for index, entry in List.indexed entries -> Keymap.KeyBinding.create (shortcut index) entry.Name (Start entry.Game)
]

let private groupedHelp = [
  [ Key.Up; Key.Down ] |> List.map KeyPress.For, "select"
  [ for index in 0 .. entries.Length - 1 -> KeyPress.For(shortcut index) ], "play directly"
]

let private bindings: Keymap.KeyBinding<int, Msg> list = [
  Keymap.KeyBinding.dynamic (Keymap.SpecialKey ConsoleKey.Enter) (fun selected -> {
    Description = "play"
    Message = Some(Start entries[selected].Game)
  })
  |> Keymap.KeyBinding.orKey (Keymap.SpecialKey ConsoleKey.Spacebar)
  Keymap.KeyBinding.create 'q' "quit" Quit
  |> Keymap.KeyBinding.orKey (Keymap.SpecialKey ConsoleKey.Escape)
]

let handleKey (key: ConsoleKeyInfo) (selected: int) =
  Keymap.KeyBinding.handleKey (groupedBindings @ shortcutBindings @ bindings) key selected

let move (delta: int) (selected: int) =
  (selected + delta + entries.Length) % entries.Length

let private keyMap (selected: int) : IKeyMap = {
  new IKeyMap with
    member _.Help() =
      seq {
        for keys, description in groupedHelp -> KeyBinding(Keys = ResizeArray keys, Help = description)
        yield! (Keymap.KeyBinding.toKeyMap bindings selected).Help()
      }
}

let private mainLayout =
  layout "main"
  |> splitHorizontally [| layout "content"; layout "help" |> withFixedSize (Some 1) |]

let private span (style: Style) (text: string) =
  Text.styledSpan (Nullable style) text

let private nameWidth = entries |> List.map _.Name.Length |> List.max

let private menuWidget (selected: int) : IWidget =
  let lines = [
    yield Text.line [ span (Style Color.Grey62) " pick a game" ]
    yield Text.line []
    for index, entry in List.indexed entries ->
      let marker, nameStyle =
        match index = selected with
        | true -> "▸", Style(Nullable Color.Aqua, Nullable(), Nullable Decoration.Bold)
        | false -> " ", Style Color.Grey85

      Text.line [
        span (Style Color.Aqua) (sprintf " %s " marker)
        span (Style Color.Grey50) (sprintf "%c  " (shortcut index))
        span nameStyle (entry.Name.PadRight nameWidth)
        span (Style Color.Grey62) (sprintf "   %s " entry.Description)
      ]
  ]

  let title = "tuigames"

  let width =
    entries
    |> List.map (fun e -> 6 + nameWidth + 3 + e.Description.Length + 1)
    |> List.max
    |> max (title.Length + 4)
    |> (+) 2

  let height = lines.Length + 2

  {
    new IWidget with
      member _.Render(ctx: RenderContext) =
        let boxed =
          box (Look.fromColor Color.Aqua)
          |> withTitle title
          |> withInnerWidget (paragraph lines :> IWidget)
          :> IWidget

        ctx.Render(
          popup (min ctx.Viewport.Width width) (min ctx.Viewport.Height height)
          |> withPopupContent boxed
          :> IWidget
        )
  }

type private View(selected: int) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let port = getPort ctx.Viewport mainLayout
      ctx.Render(menuWidget selected, port "content")
      ctx.Render(help [ keyMap selected ] |> leftAligned, port "help")

let view (selected: int) : IWidget =
  View selected :> IWidget

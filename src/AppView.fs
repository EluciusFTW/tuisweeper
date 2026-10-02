module AppView

open System
open Spectre.Console
open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets
open Game
open Application

let private mainLayout =
  layout "main"
  |> splitHorizontally [| layout "content"; layout "help" |> withFixedSize (Some 1) |]

let private hiddenBg = Color.Grey30
let private revealedBg = Color.Grey11
let private cursorBg = Color.DeepSkyBlue3

// The classic Minesweeper number palette, brightened for a dark terminal.
let private numberColor =
  function
  | 1 -> Color.DodgerBlue1
  | 2 -> Color.Green3
  | 3 -> Color.Red1
  | 4 -> Color.MediumPurple1
  | 5 -> Color.Orange1
  | 6 -> Color.DarkCyan
  | 7 -> Color.White
  | _ -> Color.Grey62

let private styled (fg: Color) (bg: Color) (text: string) =
  Text.styledSpan (Nullable(Style(Nullable fg, Nullable bg))) text

let private cellSpan (board: Board) (cursor: int * int) (pos: int * int) =
  let c = Game.cell board pos

  let fg, bg, glyph =
    match c.State, c.Mine, board.Status with
    | Revealed, true, Lost exploded when exploded = pos -> Color.White, Color.Red3, "*"
    | Revealed, true, _ -> Color.Red1, revealedBg, "*"
    | Revealed, false, _ when c.Adjacent = 0 -> revealedBg, revealedBg, " "
    | Revealed, false, _ -> numberColor c.Adjacent, revealedBg, string c.Adjacent
    // After a loss, flags on safe cells are shown as mistakes.
    | Flagged, false, Lost _ -> Color.Orange1, hiddenBg, "x"
    | Flagged, _, _ -> Color.Red1, hiddenBg, "⚑"
    | Hidden, _, _ -> Color.Grey50, hiddenBg, "·"

  let bg =
    match pos = cursor && not (Game.isOver board) with
    | true -> cursorBg
    | false -> bg

  styled fg bg (sprintf " %s " glyph)

let private statusLine (model: Model) =
  let board = model.Board
  let minesLeft = board.MineCount - Game.flagCount board
  let seconds = min 999 (int model.Elapsed.TotalSeconds)

  let face, faceColor, message =
    match board.Status with
    | Ready -> ":)", Color.Yellow, "reveal any cell to start"
    | Playing -> ":)", Color.Yellow, ""
    | Won -> "B)", Color.Green3, "cleared! n: new game"
    | Lost _ -> ":(", Color.Red1, "boom! n: try again"

  [
    Text.line [
      Text.styledSpan (Nullable(Style Color.Red1)) (sprintf " %03d " minesLeft)
      Text.span " "
      Text.styledSpan (Nullable(Style faceColor)) face
      Text.span " "
      Text.styledSpan (Nullable(Style Color.Aqua)) (sprintf " %03d " seconds)
    ]
    Text.line [ Text.styledSpan (Nullable(Style faceColor)) (sprintf " %s" message) ]
  ]

let private boardWidget (model: Model) : IWidget =
  let board = model.Board

  let rows = [
    for y in 0 .. board.Height - 1 -> Text.line [ for x in 0 .. board.Width - 1 -> cellSpan board model.Cursor (x, y) ]
  ]

  let lines = statusLine model @ rows

  let borderColor =
    match board.Status with
    | Won -> Color.Green3
    | Lost _ -> Color.Red1
    | Ready
    | Playing -> Color.Aqua

  let title = sprintf "Minesweeper · %s" (Difficulty.name model.Difficulty)

  // Three columns per cell, two status lines, plus the box border.
  let width = max (board.Width * 3) (title.Length + 4) + 2
  let height = board.Height + 2 + 2

  { new IWidget with
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

type AppView(model: Model) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let port = getPort ctx.Viewport mainLayout
      ctx.Render(boardWidget model, port "content")
      ctx.Render(help [ keyMap model ] |> leftAligned, port "help")

// Subscriptions dispatch from thread-pool threads, and the terminal is not
// thread-safe, so serialize draws (same as tuigether).
let private renderLock = obj ()

let view (renderer: Renderer) (model: Model) _dispatch =
  lock renderLock (fun () -> renderer.Draw(fun ctx _ -> ctx.Render(AppView model)))

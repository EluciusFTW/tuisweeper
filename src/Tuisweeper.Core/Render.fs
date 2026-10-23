/// Drawing primitives that don't depend on the component's model.
module internal Tuisweeper.Render

open System
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open Game

let hiddenBg = Color.Grey30
let revealedBg = Color.Grey11
let cursorBg = Color.DeepSkyBlue3

// The classic Minesweeper number palette, brightened for a dark terminal.
let numberColor =
  function
  | 1 -> Color.DodgerBlue1
  | 2 -> Color.Green3
  | 3 -> Color.Red1
  | 4 -> Color.MediumPurple1
  | 5 -> Color.Orange1
  | 6 -> Color.DarkCyan
  | 7 -> Color.White
  | _ -> Color.Grey62

let styled (fg: Color) (bg: Color) (text: string) =
  Text.styledSpan (Nullable(Style(Nullable fg, Nullable bg))) text

let cellSpan (board: Board) (cursor: int * int) (pos: int * int) =
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

let recordColor = Color.Gold1

let recordStyle = Nullable(Style(Nullable recordColor, Nullable(), Nullable Decoration.Bold))

let lineWidth (spans: (string * Nullable<Style>) list) =
  spans |> List.sumBy (fun (text, _) -> text.Length)

let toLine (spans: (string * Nullable<Style>) list) =
  Text.line [ for text, style in spans -> Text.styledSpan style text ]

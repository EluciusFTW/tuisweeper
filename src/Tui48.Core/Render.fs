/// Drawing primitives that don't depend on the component's model.
module internal Tui48.Render

open System
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open Game

// Terminal cells are roughly 2-2.5 times as tall as wide, so 8x3 tiles with 2-column/1-row gaps look square.
let tileWidth = 8
let tileHeight = 3
let gapWidth = 2
let gapHeight = 1

// The board is lighter than the terminal and its empty slots are recessed, so grid and edge stay visible.
let boardBg = Color.Grey30
let emptyBg = Color.Grey19

// From cool to hot as the tiles grow; the goal tile is gold.
let tileColors =
  function
  | 0 -> emptyBg, emptyBg
  | 2 -> Color.Grey11, Color.Grey82
  | 4 -> Color.Grey11, Color.LightSkyBlue1
  | 8 -> Color.Grey11, Color.SkyBlue1
  | 16 -> Color.White, Color.DeepSkyBlue3
  | 32 -> Color.White, Color.DodgerBlue2
  | 64 -> Color.White, Color.RoyalBlue1
  | 128 -> Color.White, Color.MediumPurple3
  | 256 -> Color.White, Color.DarkViolet
  | 512 -> Color.White, Color.Magenta3
  | 1024 -> Color.White, Color.DeepPink3
  | 2048 -> Color.Grey11, Color.Gold1
  | _ -> Color.Gold1, Color.Red3

let gridWidth (size: int) =
  size * tileWidth + (size + 1) * gapWidth

let gridHeight (size: int) =
  size * tileHeight + (size + 1) * gapHeight

let private background (color: Color) =
  Nullable(Style(Nullable(), Nullable color))

// Paints cells directly: a paragraph would trim the blank lines and trailing gaps of the grid.
let gridWidget (board: Board) : IWidget = {
  new IWidget with
    member _.Render(ctx: RenderContext) =
      ctx.SetStyle(Rectangle(0, 0, gridWidth board.Size, gridHeight board.Size), background boardBg)

      for row, tiles in List.indexed board.Tiles do
        for column, value in List.indexed tiles do
          let x = gapWidth + column * (tileWidth + gapWidth)
          let y = gapHeight + row * (tileHeight + gapHeight)
          let fg, bg = tileColors value
          ctx.SetStyle(Rectangle(x, y, tileWidth, tileHeight), background bg)

          match value with
          | 0 -> ()
          | _ ->
            let text = string value
            let style = Style(Nullable fg, Nullable bg, Nullable Decoration.Bold)

            ctx.SetString(x + (tileWidth - text.Length) / 2, y + tileHeight / 2, text, style)
            |> ignore
}

let recordColor = Color.Gold1

let recordStyle = Nullable(Style(Nullable recordColor, Nullable(), Nullable Decoration.Bold))

let lineWidth (spans: (string * Nullable<Style>) list) =
  spans |> List.sumBy (fun (text, _) -> text.Length)

let toLine (spans: (string * Nullable<Style>) list) =
  Text.line [ for text, style in spans -> Text.styledSpan style text ]

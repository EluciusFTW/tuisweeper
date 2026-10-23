module internal Tui48.Game

open System

/// Reaching this tile wins; the player may then keep going for a higher score.
let goal = 2048

module Size =
  let all = [ 3; 4; 5; 6 ]

  let name (size: int) =
    sprintf "%dx%d" size size

type Status =
  | Playing
  | Won
  | Lost

type Board = {
  Size: int
  /// Rows from top to bottom; 0 is an empty cell.
  Tiles: int list list
  Score: int
  Status: Status
  KeepGoing: bool
}

let emptyCells (tiles: int list list) = [
  for y, row in List.indexed tiles do
    for x, value in List.indexed row do
      match value with
      | 0 -> yield x, y
      | _ -> ()
]

let maxTile (tiles: int list list) =
  tiles |> List.concat |> List.max

let isOver (board: Board) =
  match board.Status with
  | Won
  | Lost -> true
  | Playing -> false

// Slides a row to the left: each tile merges at most once, the leftmost pair first.
let private slideRow (row: int list) =
  let rec merge acc gained =
    function
    | a :: b :: rest when a = b -> merge (a + b :: acc) (gained + a + b) rest
    | a :: rest -> merge (a :: acc) gained rest
    | [] -> List.rev acc, gained

  let merged, gained = row |> List.filter ((<>) 0) |> merge [] 0
  merged @ List.replicate (row.Length - merged.Length) 0, gained

let private reversed = List.map List.rev

// Turns the board so that `direction` becomes a slide to the left, and back.
let private orient =
  function
  | Left -> id, id
  | Right -> reversed, reversed
  | Up -> List.transpose, List.transpose
  | Down -> List.transpose >> reversed, reversed >> List.transpose

let private slideTiles (direction: SlideDirection) (tiles: int list list) =
  let into, back = orient direction
  let rows = into tiles |> List.map slideRow
  rows |> List.map fst |> back, rows |> List.sumBy snd

let private canMove (tiles: int list list) =
  [ Left; Right; Up; Down ]
  |> List.exists (fun direction -> fst (slideTiles direction tiles) <> tiles)

// A new tile on a random empty cell: a 2, or a 4 one time in ten.
let private spawn (rng: Random) (tiles: int list list) =
  match emptyCells tiles with
  | [] -> tiles
  | cells ->
    let x, y = cells[rng.Next cells.Length]

    let value =
      match rng.Next 10 with
      | 0 -> 4
      | _ -> 2

    tiles |> List.updateAt y (tiles[y] |> List.updateAt x value)

let create (rng: Random) (size: int) : Board = {
  Size = size
  Tiles = List.replicate size (List.replicate size 0) |> spawn rng |> spawn rng
  Score = 0
  Status = Playing
  KeepGoing = false
}

let slide (rng: Random) (direction: SlideDirection) (board: Board) =
  match board.Status with
  | Won
  | Lost -> board
  | Playing ->
    let tiles, gained = slideTiles direction board.Tiles

    match tiles = board.Tiles with
    | true -> board
    | false ->
      let tiles = spawn rng tiles

      let status =
        match not board.KeepGoing && maxTile tiles >= goal, canMove tiles with
        | true, _ -> Won
        | false, true -> Playing
        | false, false -> Lost

      {
        board with
            Tiles = tiles
            Score = board.Score + gained
            Status = status
      }

/// After reaching the goal, continue playing on the same board.
let keepGoing (board: Board) =
  match board.Status with
  | Won -> {
      board with
          KeepGoing = true
          Status =
            match canMove board.Tiles with
            | true -> Playing
            | false -> Lost
    }
  | Playing
  | Lost -> board

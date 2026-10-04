module Game

open System

type Difficulty =
  | Beginner
  | Intermediate
  | Expert

module Difficulty =
  let all = [ Beginner; Intermediate; Expert ]

  let dimensions =
    function
    | Beginner -> 9, 9, 10
    | Intermediate -> 16, 16, 40
    | Expert -> 30, 16, 99

  let name =
    function
    | Beginner -> "Beginner"
    | Intermediate -> "Intermediate"
    | Expert -> "Expert"

type CellState =
  | Hidden
  | Flagged
  | Revealed

type Cell = {
  Mine: bool
  Adjacent: int
  State: CellState
}

type Status =
  | Ready
  | Playing
  | Won
  | Lost of exploded: (int * int)

type Board = {
  Width: int
  Height: int
  MineCount: int
  Cells: Map<int * int, Cell>
  Status: Status
}

let private emptyCell = {
  Mine = false
  Adjacent = 0
  State = Hidden
}

let create (difficulty: Difficulty) : Board =
  let width, height, mines = Difficulty.dimensions difficulty

  {
    Width = width
    Height = height
    MineCount = mines
    Cells =
      Map [
        for y in 0 .. height - 1 do
          for x in 0 .. width - 1 -> (x, y), emptyCell
      ]
    Status = Ready
  }

let neighbours (board: Board) (x: int, y: int) = [
  for dy in -1 .. 1 do
    for dx in -1 .. 1 do
      let nx, ny = x + dx, y + dy

      match
        (dx, dy) <> (0, 0)
        && nx >= 0
        && ny >= 0
        && nx < board.Width
        && ny < board.Height
      with
      | true -> yield nx, ny
      | false -> ()
]

let cell (board: Board) pos =
  board.Cells[pos]

let flagCount (board: Board) =
  board.Cells |> Map.filter (fun _ c -> c.State = Flagged) |> Map.count

let isOver (board: Board) =
  match board.Status with
  | Won
  | Lost _ -> true
  | Ready
  | Playing -> false

let private placeMines (rng: Random) (safe: int * int) (board: Board) =
  let safeZone =
    let zone = safe :: neighbours board safe |> Set.ofList

    match board.Width * board.Height - zone.Count >= board.MineCount with
    | true -> zone
    | false -> Set.singleton safe

  let mines =
    board.Cells
    |> Map.keys
    |> Seq.filter (fun pos -> not (safeZone.Contains pos))
    |> Seq.toArray
    |> fun candidates ->
        rng.Shuffle candidates
        candidates |> Array.truncate board.MineCount |> Set.ofArray

  let cells =
    board.Cells
    |> Map.map (fun pos c -> {
      c with
          Mine = mines.Contains pos
          Adjacent = neighbours board pos |> List.filter mines.Contains |> List.length
    })

  {
    board with
        Cells = cells
        Status = Playing
  }

let private checkWin (board: Board) =
  let allSafeRevealed = board.Cells |> Map.forall (fun _ c -> c.Mine || c.State = Revealed)

  match allSafeRevealed with
  | true -> {
      board with
          Status = Won
          Cells =
            board.Cells
            |> Map.map (fun _ c ->
              match c.Mine with
              | true -> { c with State = Flagged }
              | false -> c)
    }
  | false -> board

let private explode (pos: int * int) (board: Board) = {
  board with
      Status = Lost pos
      Cells =
        board.Cells
        |> Map.map (fun _ c ->
          match c.Mine, c.State with
          | true, Hidden -> { c with State = Revealed }
          | _ -> c)
}

let private floodReveal (start: (int * int) list) (board: Board) =
  let rec loop (queue: (int * int) list) (cells: Map<int * int, Cell>) =
    match queue with
    | [] -> cells
    | pos :: rest ->
      let c = cells[pos]

      match c.State with
      | Hidden ->
        let cells = cells |> Map.add pos { c with State = Revealed }

        match c.Adjacent with
        | 0 -> loop (rest @ neighbours board pos) cells
        | _ -> loop rest cells
      | Flagged
      | Revealed -> loop rest cells

  {
    board with
        Cells = loop start board.Cells
  }

let private revealCells (positions: (int * int) list) (board: Board) =
  match
    positions
    |> List.tryFind (fun pos -> let c = cell board pos in c.Mine && c.State = Hidden)
  with
  | Some mine -> explode mine board
  | None -> board |> floodReveal positions |> checkWin

let reveal (rng: Random) (pos: int * int) (board: Board) =
  match board.Status with
  | Won
  | Lost _ -> board
  | Ready -> board |> placeMines rng pos |> revealCells [ pos ]
  | Playing ->
    let c = cell board pos

    match c.State with
    | Flagged -> board
    | Hidden -> revealCells [ pos ] board
    | Revealed ->
      let around = neighbours board pos
      let flagged = around |> List.filter (fun p -> (cell board p).State = Flagged) |> List.length

      match c.Adjacent > 0 && flagged = c.Adjacent with
      | true -> revealCells (around |> List.filter (fun p -> (cell board p).State = Hidden)) board
      | false -> board

let toggleFlag (pos: int * int) (board: Board) =
  match isOver board with
  | true -> board
  | false ->
    let c = cell board pos

    let next =
      match c.State with
      | Hidden -> Some Flagged
      | Flagged -> Some Hidden
      | Revealed -> None

    match next with
    | Some state -> {
        board with
            Cells = board.Cells |> Map.add pos { c with State = state }
      }
    | None -> board

module Tui48.Highscore

open System
open System.IO
open Game

/// Best score per board size (4 for 4x4).
type Highscores = Map<int, int>

/// Where highscores are kept. A host passes one in `Tui48.Dependencies`; the component
/// loads from it in `init` and saves to it when a game with a new record ends.
type Store = {
  Load: unit -> Highscores
  Save: Highscores -> unit
}

// ~/.config/tui48 on Linux, %APPDATA%\tui48 on Windows. ApplicationData can
// come back empty (e.g. no usable HOME); then fall back, and never write relative to the cwd.
let private defaultPath =
  let folder = Environment.GetFolderPath Environment.SpecialFolder.ApplicationData

  let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile

  [
    folder
    (if String.IsNullOrEmpty home then
       ""
     else
       Path.Combine(home, ".config"))
  ]
  |> List.tryFind Path.IsPathRooted
  |> Option.map (fun dir -> Path.Combine(dir, "tui48", "highscores.txt"))

let private parseLine (line: string) =
  match line.Split(' ', StringSplitOptions.RemoveEmptyEntries) with
  | [| name; score |] ->
    match Size.all |> List.tryFind (fun s -> Size.name s = name), Int32.TryParse score with
    | Some size, (true, score) when score > 0 -> Some(size, score)
    | _ -> None
  | _ -> None

// A missing or corrupt file just means no highscores yet; it must never stop the game.
let private loadFrom (path: string) () : Highscores =
  try
    match File.Exists path with
    | true -> File.ReadAllLines path |> Array.choose parseLine |> Map.ofArray
    | false -> Map.empty
  with _ ->
    Map.empty

let private saveTo (path: string) (scores: Highscores) =
  try
    Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore

    scores
    |> Map.toList
    |> List.map (fun (size, score) -> sprintf "%s %d" (Size.name size) score)
    |> fun lines -> File.WriteAllLines(path, lines)
  with _ ->
    ()

module Store =
  /// Keeps nothing: every session starts without highscores.
  let none: Store = {
    Load = fun () -> Map.empty
    Save = ignore
  }

  /// A plain-text file at `path`, one "4x4 score" line per record.
  let file (path: string) : Store = {
    Load = loadFrom path
    Save = saveTo path
  }

  /// The standalone game's file (~/.config/tui48/highscores.txt or %APPDATA%\tui48),
  /// so records are shared with it. Falls back to `none` if no user folder can be found.
  let defaultFile: Store = defaultPath |> Option.map file |> Option.defaultValue none

let internal best (scores: Highscores) (size: int) =
  Map.tryFind size scores |> Option.defaultValue 0

let internal isRecord (scores: Highscores) (size: int) (score: int) =
  score > best scores size

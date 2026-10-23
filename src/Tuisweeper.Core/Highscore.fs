module Tuisweeper.Highscore

open System
open System.IO
open Game

/// Best (fastest) winning time per difficulty.
type Highscores = Map<Difficulty, TimeSpan>

/// Where highscores are kept. A host passes one in `Tuisweeper.Dependencies`; the component
/// loads from it in `init` and saves to it on every new record.
type Store = {
  Load: unit -> Highscores
  Save: Highscores -> unit
}

// ~/.config/tuisweeper on Linux, %APPDATA%\tuisweeper on Windows. ApplicationData can
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
  |> Option.map (fun dir -> Path.Combine(dir, "tuisweeper", "highscores.txt"))

let private parseLine (line: string) =
  match line.Split(' ', StringSplitOptions.RemoveEmptyEntries) with
  | [| name; millis |] ->
    match Difficulty.all |> List.tryFind (fun d -> Difficulty.name d = name), Int64.TryParse millis with
    | Some difficulty, (true, ms) when ms > 0L -> Some(difficulty, TimeSpan.FromMilliseconds(float ms))
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
    |> List.map (fun (d, t) -> sprintf "%s %d" (Difficulty.name d) (int64 t.TotalMilliseconds))
    |> fun lines -> File.WriteAllLines(path, lines)
  with _ ->
    ()

module Store =
  /// Keeps nothing: every session starts without highscores.
  let none: Store = {
    Load = fun () -> Map.empty
    Save = ignore
  }

  /// A plain-text file at `path`, one "Difficulty milliseconds" line per record.
  let file (path: string) : Store = { Load = loadFrom path; Save = saveTo path }

  /// The standalone game's file (~/.config/tuisweeper/highscores.txt or %APPDATA%\tuisweeper),
  /// so records are shared with it. Falls back to `none` if no user folder can be found.
  let defaultFile: Store = defaultPath |> Option.map file |> Option.defaultValue none

let internal isRecord (scores: Highscores) (difficulty: Difficulty) (time: TimeSpan) =
  match Map.tryFind difficulty scores with
  | Some best -> time < best
  | None -> true

let internal format (time: TimeSpan) =
  sprintf "%.1fs" (min 999.9 time.TotalSeconds)

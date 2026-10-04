module Highscore

open System
open System.IO
open Game

/// Best (fastest) winning time per difficulty.
type Highscores = Map<Difficulty, TimeSpan>

// ~/.config/tuisweeper on Linux, %APPDATA%\tuisweeper on Windows. ApplicationData can
// come back empty (e.g. no usable HOME); then fall back, and never write relative to the cwd.
let private path =
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
let load () : Highscores =
  try
    match path with
    | Some path when File.Exists path -> File.ReadAllLines path |> Array.choose parseLine |> Map.ofArray
    | _ -> Map.empty
  with _ ->
    Map.empty

let save (scores: Highscores) =
  try
    path
    |> Option.iter (fun path ->
      Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore

      scores
      |> Map.toList
      |> List.map (fun (d, t) -> sprintf "%s %d" (Difficulty.name d) (int64 t.TotalMilliseconds))
      |> fun lines -> File.WriteAllLines(path, lines))
  with _ ->
    ()

let isRecord (scores: Highscores) (difficulty: Difficulty) (time: TimeSpan) =
  match Map.tryFind difficulty scores with
  | Some best -> time < best
  | None -> true

let format (time: TimeSpan) =
  sprintf "%.1fs" (min 999.9 time.TotalSeconds)

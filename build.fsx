#r @"packages/FAKE/tools/FakeLib.dll"

open Fake

Target "Build" (fun _ ->
    !! "./src/FsDynamics.fsproj"
    |> MSBuildDebug "" "Build"
    |> ignore
)

RunTargetOrDefault "Build"
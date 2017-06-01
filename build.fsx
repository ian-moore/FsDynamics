#r @"packages/FAKE/tools/FakeLib.dll"

open Fake

Target "Build" (fun _ ->
    !! "./src/FsDynamics.fsproj"
    |> MSBuildRelease "" "Build"
    |> ignore
)

Target "NuGet" (fun _ ->
    Paket.Pack (fun defaults ->
        { defaults with
            WorkingDir = "./src" 
            OutputPath = "../deploy" })
)

Target "Release" DoNothing

"Build"
    ==> "NuGet"
    ==> "Release"

RunTargetOrDefault "Build"
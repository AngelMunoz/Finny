open Argu
open Nacre


[<EntryPoint>]
let main args =
    let parser = ArgumentParser.Create<TestRunnerArgs>()


    let parsed =
        parser.ParseCommandLine(inputs = args, raiseOnUsage = true, ignoreMissing = true)

    let options = TestRunnerArgs.ToOptions parsed

    Commands.runTests options
    |> Async.AwaitTask
    |> Async.RunSynchronously

    0

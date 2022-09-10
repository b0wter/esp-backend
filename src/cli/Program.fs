namespace Gerlinde.Portal.Cli

open Argu
open System

module Program =
    let private parseCommandLineArguments argv =
        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> None)
        let parser = ArgumentParser.Create<Arguments.MainArgs>(errorHandler = errorHandler)
        (parser, parser.ParseCommandLine(inputs = argv, raiseOnUsage = true))

    [<EntryPoint>]
    let main argv =
        let parser, results = parseCommandLineArguments argv
        do printfn "%A" results
        0

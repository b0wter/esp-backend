namespace Gerlinde.Portal.Cli

open System

module Console =
    let rec retryIfEmpty (f: unit -> string) (errorText: string) =
        let result = f ()
        if result |> String.IsNullOrWhiteSpace then
            do printfn $"%s{errorText}"
            retryIfEmpty f errorText
        else
            result

    let rec retryIf (f: unit -> string) (predicate: string -> bool * string) =
        let result = f ()
        let isOk, errorReason = result |> predicate
        if isOk then
            result
        else
            do printfn $"%s{errorReason}"
            retryIf f predicate
            
    let readLine text =
        do printfn $"%s{text}"
        Console.ReadLine ()
        
    let readLineHidden text =
        printfn $"%s{text}"
        let rec step (aggregator: string) : string =
            let key = Console.ReadKey true
            match key.Key with
            | ConsoleKey.Backspace when aggregator.Length > 0 ->
                step (aggregator.Remove(aggregator.Length - 1, 1))
            | ConsoleKey.Enter ->
                aggregator
            | ConsoleKey.Backspace ->
                step aggregator
            | _ ->
                step (aggregator + key.KeyChar.ToString())
        step String.Empty

    


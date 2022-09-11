// Learn more about F# at http://fsharp.org

open System
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Repository


let readInput message newline retrieveInput argument =
    do printf "%s" message
    let result = retrieveInput argument
    if newline then do printfn "" else do ()
    result


let readPassword () =
    let rec step (accumulator: String) =
        let key = Console.ReadKey(true)
        if key.Key = ConsoleKey.Enter then accumulator
        else if key.Key = ConsoleKey.Backspace && accumulator.Length > 0 then step (accumulator.Substring(0, accumulator.Length - 1))
        else step (accumulator + key.KeyChar.ToString())
    readInput "Password: " true step ""


let readName () =
    readInput "Organization name: " false Console.ReadLine ()


let readEmail () = 
    readInput "Email: " false Console.ReadLine ()


let readCreateAccessToken () =
    let input = readInput "Create access token? [y/N]: " false Console.ReadLine ()
    (input = "Y" || input = "y")

let printSerialized s =
    do printfn "-------- database entry --------"
    do printfn "%s" s
    do printfn "----------------------------------"


[<EntryPoint>]
let main _ =
    let name, email, password, shouldCreateAccessToken = (readName (), readEmail(), readPassword (), readCreateAccessToken ())
    let org = Organization.create email password name
    let accessTokens =
        if shouldCreateAccessToken then [ Organization.createToken (Some ("auth cli tool - " + DateTime.Now.ToString())) ]
        else []
    let org = { org with AccessTokens = accessTokens }
    let entity = org |> Organization.toEntity
    let serialized =
        b0wter.CouchDb.Lib.Core.serializeAsJson
            [
                FifteenBelow.Json.OptionConverter() :> Newtonsoft.Json.JsonConverter
                FifteenBelow.Json.UnionConverter() :> Newtonsoft.Json.JsonConverter
            ] entity
    do printSerialized serialized
    0 // return an integer exit code

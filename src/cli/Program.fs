namespace Gerlinde.Portal.Cli

open System.Threading.Tasks
open Argu
open System

module Program =
    let private parseCommandLineArguments argv =
        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> None)
        let parser = ArgumentParser.Create<Arguments.MainArgs>(errorHandler = errorHandler)
        (parser, parser.ParseCommandLine(inputs = argv, raiseOnUsage = true))

    let login (config: Config.LoginConfig) (baseUrl: string) : Task<Result<unit, string>> =
        let rec retryIfEmpty (f: unit -> string) (errorText: string) =
            let result = f ()
            if result |> String.IsNullOrWhiteSpace then
                do printfn $"%s{errorText}"
                retryIfEmpty f errorText
            else
                result
                
        let fromConsole text =
            do printfn $"%s{text}"
            Console.ReadLine ()
            
        let fromConsoleHidden text =
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

        task {
            let email = config.Email |> Option.defaultWith (fun () -> (retryIfEmpty (fun () -> fromConsole "Please enter your email address:") "Email must not be empty"))
            let password = config.Password |> Option.defaultWith (fun () -> (retryIfEmpty (fun () -> fromConsoleHidden "Please enter your password:") "Password must not be empty"))
            
            match! Portal.login baseUrl email password with
            | Http.ApiHttpResponse.Ok content ->
                printfn "The login was successful. Do you want to store the access token in your config files? [y/N]"
                printfn "WARNING: by default the token is readable by anyone that can access your home '.config' folder."
                printfn "WARNING: this will overwrite any existing access token"
                
                match Console.ReadLine() with
                | "y" | "Y" ->
                    let storageFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                    let storagePath = System.IO.Path.Combine (storageFolder, "access_token")
                    System.IO.File.WriteAllText (storagePath, content)
                | _ ->
                    printfn $"The access token is: %s{content}"
                return Ok ()
                
            | Http.ApiHttpResponse.Error (statusCode, body, _) ->
                return Error $"The response has a non-success status code %i{statusCode} with the following reason: '%s{body}'"
                
            | Http.ApiHttpResponse.Exception exn ->
                return Error $"An exception was raised while trying to access the portal api. Details: %s{exn.Message}"
        }
        
    let tryGetAccessToken () =
        let storageFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        let storagePath = System.IO.Path.Combine (storageFolder, "access_token")
        if storagePath |> System.IO.File.Exists then
            printfn "Found an existing access token"
            storagePath |> System.IO.File.ReadAllText |> Some
        else
            printfn "No existing access token found"
            None
        
    [<EntryPoint>]
    let main argv =
        let _, results = parseCommandLineArguments argv
        let config = Config.applyAllArgs results
        let config = { config with AccessToken = tryGetAccessToken () }
        let baseUrl = "http://localhost:5000/"
        
        let task =
            match config.Command with
            | Config.Command.Login config ->
                login config baseUrl
            | Config.Command.Logout ->
                failwith "not implemented"
            | Config.Command.Uninitialized ->
                failwith "The configuration process did not produce a valid configuration/command. Please contact the developer."
            | Config.Command.AddDevice config ->
                failwith "not implemented"
            | Config.Command.DeleteDevice config ->
                failwith "not implemented"
            | Config.Command.ListDevices ->
                failwith "not implemented"
            | Config.Command.AddOrganization config ->
                failwith "not implemented"
            | Config.Command.ShowOrganization ->
                failwith "not implemented"
                
        let result = task.GetAwaiter().GetResult()
        
        match result with
        | Ok () -> 0
        | Error e ->
            printfn "%s" e
            1
namespace Gerlinde.Portal.Cli

open System.Threading.Tasks
open Argu
open System
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Lib.Json
open Newtonsoft.Json

module Program =
    let private applicationDataPath =
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gerlinde"
            )
         
    let private accessTokenPath =
        System.IO.Path.Combine(
            applicationDataPath,
            "access_token")
 
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
                    System.IO.File.WriteAllText (accessTokenPath, content)
                | _ ->
                    printfn $"The access token is: %s{content}"
                return Ok ()
                
            | Http.ApiHttpResponse.Error (statusCode, body, _) ->
                return Error $"The response has a non-success status code %i{statusCode} with the following reason: '%s{body}'"
                
            | Http.ApiHttpResponse.Exception exn ->
                return Error $"An exception was raised while trying to access the portal api. Details: %s{exn.Message}"
        }
        
    let logout authToken baseUrl =
        task {
            let! result = Portal.logout baseUrl authToken
            
            match result with
            | Http.ApiHttpResponse.Ok _ ->
                printfn "Access token has been invalidated by the backend"
                if System.IO.File.Exists accessTokenPath then
                    do accessTokenPath |> System.IO.File.Delete
                    printfn "Deleted local access token file"
                    return Ok ()
                else
                    return Ok ()
            | Http.ApiHttpResponse.Error (statusCode, body, _) ->
                return Error $"You could not be logged out because the portal returned a non-success status code %i{statusCode} with the reason: %s{body}"
            | Http.ApiHttpResponse.Exception exn ->
                return Error $"An exception was thrown because: %s{exn.Message}"
        }
        
    let addDevice authToken baseUrl (config: Config.AddDeviceConfig) =
        task {
            let device = {| MacAddress = config.MacAddress; Name = config.Name; Description = config.Description |}
            let! result = Portal.addDevice baseUrl authToken device
            
            match result with
            | Http.ApiHttpResponse.Ok device ->
                printfn "%s" device
                return Ok ()
            | Http.ApiHttpResponse.Error (statusCode, body, _) ->
                return Error $"Device was not saved because the portal returned a non-success status code %i{statusCode} with the reason: %s{body}"
            | Http.ApiHttpResponse.Exception exn ->
                return Error $"An exception was thrown because: %s{exn.Message}"
        }
        
    let deleteDevice authToken baseUrl macAddress =
        task {
            let! result = Portal.deleteDevice baseUrl authToken macAddress
            match result with
            | Http.ApiHttpResponse.Ok _ ->
                printfn "Device has been deleted"
                return Ok ()
            | Http.ApiHttpResponse.Error (statusCode, body, _) ->
                return Error $"Device was not deleted because the portal returned a non-success status code %i{statusCode} with the reason: %s{body}"
            | Http.ApiHttpResponse.Exception exn ->
                return Error $"An exception was thrown because: %s{exn.Message}"
        }

    let deviceDetails authToken baseUrl macAddress =
        task {
            let! result = Portal.deviceDetails baseUrl authToken macAddress
            match result with
            | Http.ApiHttpResponse.Ok details ->
                printfn $"%s{details}"
                return Ok ()
            | Http.ApiHttpResponse.Error (statusCode, body, _) ->
                return Error $"Could not retrieve device details because the portal returned a non-success status code %i{statusCode} with the reason: %s{body}"
            | Http.ApiHttpResponse.Exception exn ->
                return Error $"An exception was thrown because: %s{exn.Message}"
        }
        
    let invalidAuthToken (token: Organization.AccessToken) =
        printfn $"Your current access token was only valid until %A{token.ValidThrough}. You need to retrieve a new one."
        printfn "Do you want to remove the local (invalid) access token? [y/N]"
        let input = Console.ReadLine ()
        do match input with
            | "Y" | "y" ->
                System.IO.File.Delete accessTokenPath
            | _ ->
                ()
        printfn "To use with application you need to login via the 'login' command"
        
    let tryGetAccessToken () =
        let deserialize s =
            Newtonsoft.Json.JsonConvert.DeserializeObject<Organization.AccessToken>(s, Json.DateOnlyJsonConverter())
        if accessTokenPath |> System.IO.File.Exists then
            accessTokenPath
            |> System.IO.File.ReadAllText
            |> deserialize
            |> Some
        else
            None
            
    let ``Make sure application data folder exists`` () =
        if System.IO.Directory.Exists applicationDataPath then ()
        else System.IO.Directory.CreateDirectory applicationDataPath |> ignore
        
    let ``Change to invalid token command if token expired`` (config: Config.Config) =
        match config.AccessToken with
        | Some token ->
            if token.ValidThrough >= DateOnly.FromDateTime(DateTime.Now) then config
            else { config with Command = Config.Command.InvalidAuthToken token }
        | None ->
            config
        
    [<EntryPoint>]
    let main argv =
        let _, results = parseCommandLineArguments argv
        let config = Config.applyAllArgs results
        do ``Make sure application data folder exists`` ()

        let config =
            { config with AccessToken = tryGetAccessToken () }
            |> ``Change to invalid token command if token expired``

        let baseUrl = "http://localhost:5000/"
        
        let ``Run action or fail if no auth token is set`` (accessToken: Organization.AccessToken option) f =
            match accessToken with
            | Some token -> f token.Token
            | None -> Task.FromResult(Error "You need to login first")
        
        let task =
            match config.Command with
            | Config.Command.Login config ->
                login config baseUrl
            | Config.Command.Logout ->
                ``Run action or fail if no auth token is set``
                    config.AccessToken
                    (fun token -> logout token baseUrl)
            | Config.Command.Status ->
                if config.AccessToken.IsSome then printfn "An access token is set"
                else printfn "No access token is set, you need to login"
                Task.FromResult(Ok ())
            | Config.Command.Uninitialized ->
                failwith "The configuration process did not produce a valid configuration/command. Please contact the developer."
            | Config.Command.InvalidAuthToken token ->
                do invalidAuthToken token
                Task.FromResult(Ok ())
            | Config.Command.AddDevice addConfig ->
                ``Run action or fail if no auth token is set``
                    config.AccessToken
                    (fun token -> addDevice token baseUrl addConfig)
            | Config.Command.DeleteDevice macAddress ->
                ``Run action or fail if no auth token is set``
                    config.AccessToken
                    (fun token -> deleteDevice token baseUrl macAddress)
            | Config.Command.DeviceDetails macAddress ->
                ``Run action or fail if no auth token is set``
                    config.AccessToken
                    (fun token -> deviceDetails token baseUrl macAddress)
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
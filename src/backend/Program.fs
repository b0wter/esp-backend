module Gerlinde.Esp.Backend.App

open FsToolkit.ErrorHandling
open Gerlinde.Esp.Backend
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Repository
open Gerlinde.Shared.WebApi
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Newtonsoft.Json
open System
open System.Collections.Immutable
open System.Dynamic
open System.IO
open System.Threading.Tasks

let private deviceAccessTokenFile = "device_access_tokens.json"
let DeviceAccessTokens =
    if deviceAccessTokenFile |> File.Exists then
        try
            deviceAccessTokenFile |> File.ReadAllText |> System.Text.Json.JsonSerializer.Deserialize<ImmutableDictionary<string, Device.DeviceIdentification>>
        with
        | :? System.Text.Json.JsonException as ex ->
            failwithf $"The device access token file '%s{deviceAccessTokenFile}' is corrupt. Reason: %s{Environment.NewLine}%s{ex.Message}"
    else
        printfn $"The device access token file '%s{deviceAccessTokenFile}' could not be found. Creating a new dictionary."
        ImmutableDictionary<string, Device.DeviceIdentification>.Empty
        
// ---------------------------------
// Web app
// ---------------------------------
let mustBeAuthenticatedDevice (successHandler: (Device.Device * Guid) -> HttpHandler) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            match ctx.Request.Headers.Authorization |> Seq.tryHead with
            | Some token ->
                let token = token.Substring(6).TrimEnd().TrimStart()
                let repo = ctx.GetService<CouchDb.C>()
                match! repo.FindDeviceByAccessToken token with
                | Ok deviceWithOrganizationId ->
                    return! successHandler deviceWithOrganizationId next ctx
                | Error e ->
                    do ctx.SetStatusCode 404
                    return! ctx.WriteTextAsync e
            | None ->
                ctx.SetStatusCode 401
                return! ctx.WriteTextAsync "The request is missing the bearer token"
        }
        
let jsonParsingError message =
    setStatusCode 400 >=> json message
    
let defaultBindJson<'a> = Json.tryBindJson<'a> jsonParsingError

let defaultBindJsonWithArg<'a, 'b> = Json.tryBindJsonWithExtra<'a, 'b> jsonParsingError

let registrationHandler (registration : Device.DeviceRegistrationRequest) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) ->
        taskResult {
            let repo = ctx.GetService<CouchDb.C>()
            let! organization = registration.OrganisationAccessToken |> repo.FindOrganizationByAccessToken
            let deviceToken = (Utilities.generateToken 64)
            let device = Device.fromRegistration registration deviceToken
            do! repo.RegisterDevice organization.Id device |> AsyncResult.ignore
            do ctx.SetStatusCode 201
            return! ctx.WriteTextAsync deviceToken
        }
        |> Handler.mapErrorToResponse ctx

let getLatestFirmwareVersion (device: Device.Device, _) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) ->
        task {
            match device.CurrentFirmwareVersion with
            | Some firmware ->
                return! ctx.WriteJsonAsync firmware
            | None ->
                return! ctx.WriteJsonAsync {| error = "No firmware available for the given device"  |}
        }
        
let getOutstandingCommands (device: Device.Device, _) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) ->
        task {
            return! ctx.WriteJsonAsync device.OutstandingCommands
        }

let markCommandsFinished (commands: Device.RemoteCommandResult list) (device: Device.Device, organizationId: Guid) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        taskResult {
            let finishedCommands =
                commands |> List.map (fun command ->
                    let request =
                        device.OutstandingCommands
                        |> List.tryFind (fun outstanding -> outstanding.Id = command.Id)
                    let name, requestedAt =
                        request
                        |> Option.map (fun r -> (r.Command, Some r.RequestedAt))
                        |> Option.defaultValue ("unknown", None)
                    {
                        Device.FinishedCommand.Id = command.Id
                        Device.FinishedCommand.Details = command.Details
                        Device.FinishedCommand.Name = name
                        Device.FinishedCommand.FinishedAt = DateTime.Now
                        Device.FinishedCommand.IsSuccess = command.Success
                        Device.FinishedCommand.RequestedAt = requestedAt
                        Device.FinishedCommand.WasKnownRequest = request.IsSome
                    })
            let updatedDevice =
                {
                    device with
                        FinishedCommands = device.FinishedCommands @ finishedCommands
                        OutstandingCommands =
                            device.OutstandingCommands
                            |> List.where (fun o -> not (finishedCommands |> List.exists (fun f -> f.Id = o.Id)))
                }
            let repo = ctx.GetService<CouchDb.C>()
            let! _ = repo.SaveDevice (updatedDevice, organizationId)
            return! next ctx
        }
        |> Handler.mapErrorToResponse ctx

let addStatusUpdate (status: ExpandoObject) (device: Device.Device, organizationId: Guid) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        taskResult {
            let statusUpdate = { Device.StatusUpdate.Timestamp = DateTime.Now; Device.StatusUpdate.Status = status }
            let updatedDevice = { device with StatusUpdates = statusUpdate :: device.StatusUpdates }
            let repo = ctx.GetService<CouchDb.C>()
            let! _ = repo.SaveDevice (updatedDevice, organizationId)
            let latestFirmware = updatedDevice |> Device.latestAvailableFirmware
            return! ctx.WriteJsonAsync({| OutstandingCommands = updatedDevice.OutstandingCommands; Firmware = latestFirmware |})
        }
        |> Handler.mapErrorToResponse ctx
    
let updateDevice (update: Device.DeviceUpdate) (device: Device.Device, organizationId: Guid) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        taskResult {
            let updatedCommands = update.KnownCommands |> Option.defaultValue []
            let updatedDevice = { device with CurrentFirmwareVersion = Some update.FirmwareVersion; KnownCommands = updatedCommands }
            
            let repo = ctx.GetService<CouchDb.C>()
            let! _ = repo.SaveDevice (updatedDevice, organizationId)
            return! next ctx
        }
        |> Handler.mapErrorToResponse ctx
    

let webApp =
    choose [
        GET >=>
            choose [
                route "/latestfirmwareversion" >=> mustBeAuthenticatedDevice getLatestFirmwareVersion
                route "/outstandingcommands" >=> mustBeAuthenticatedDevice getOutstandingCommands
            ]
        POST >=>
            choose [
                route "/registration" >=>  defaultBindJson Validation.deviceRegistrationRequest registrationHandler
                route "/markcommandsfinished" >=> mustBeAuthenticatedDevice (defaultBindJsonWithArg Validation.markCommandsFinished markCommandsFinished)
                route "/statusupdate" >=> mustBeAuthenticatedDevice (defaultBindJsonWithArg Validation.statusUpdate addStatusUpdate)
            ]
        PUT >=>
            choose [
                route "/updatedevice" >=> mustBeAuthenticatedDevice (defaultBindJsonWithArg Validation.updateDevice updateDevice)
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

type DataBaseConfiguration(config: IConfiguration) =
    interface CouchDb.IDatabaseConfiguration with
        member this.Host = config.GetSection("CouchDb").GetValue<string>("Host")
        member this.Port = config.GetSection("CouchDb").GetValue<int>("Port")
        member this.DeviceDatabaseName = config.GetSection("CouchDb").GetValue<string>("Devices")
        member this.OrganizationDatabaseName = config.GetSection("CouchDb").GetValue<string>("Organizations")
        member this.Username = config.GetSection("CouchDb").GetValue<string>("Username")
        member this.Password = config.GetSection("CouchDb").GetValue<string>("Password")

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    services.AddSingleton<CouchDb.C>() |> ignore
    let customJsonSettings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)
    do customJsonSettings.Converters.Add(FifteenBelow.Json.OptionConverter() :> JsonConverter)
    do customJsonSettings.Converters.Add(FifteenBelow.Json.UnionConverter() :> JsonConverter)
    services.AddSingleton<Json.ISerializer>(
        NewtonsoftJson.Serializer(customJsonSettings)) |> ignore
    services.AddSingleton<CouchDb.IDatabaseConfiguration, DataBaseConfiguration>() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
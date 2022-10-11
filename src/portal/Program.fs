module Gerlinde.Portal.Backend.App

open System
open System.IO
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Repository
open Gerlinde.Shared.WebApi
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open FsToolkit.ErrorHandling
open Newtonsoft.Json

// ---------------------------------
// Web app
// ---------------------------------
let mustBeAuthenticated (successHandler: (Organization.Organization * string) -> HttpHandler) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            match ctx.Request.Headers.Authorization |> Seq.tryHead with
            | Some token ->
                let token = token.Substring(6).TrimEnd().TrimStart()
                let repo = ctx.GetService<CouchDb.C>()
                match! repo.FindOrganizationByAccessToken token with
                | Ok org ->
                    let orgToken = org.AccessTokens |> List.find (fun t -> t.Token = token)
                    if orgToken.ValidThrough >= DateOnly.FromDateTime(DateTime.Now) then
                        return! successHandler (org, token) next ctx
                    else
                        do ctx.SetStatusCode 401
                        return! ctx.WriteTextAsync "The access token is no longer valid"
                | Error e ->
                    do ctx.SetStatusCode 401
                    return! ctx.WriteTextAsync e
            | None ->
                ctx.SetStatusCode 401
                return! ctx.WriteTextAsync "The request is missing the bearer token"
        }

let jsonParsingError message =
    setStatusCode 400 >=> json message
    
let defaultBindJson<'a> = Json.tryBindJson<'a> jsonParsingError

let defaultBindJsonWithArg<'a, 'b> = Json.tryBindJsonWithExtra<'a, 'b> jsonParsingError

let defaultBindJsonAndValidate<'a, 'b> = Json.tryBindJsonAndTransform<'a, 'b> jsonParsingError

let defaultBindJsonAndTransformWithArg<'payload, 'entity, 'extra> = Json.tryBindJsonAndTransformWithExtra<'payload, 'entity, 'extra> jsonParsingError

let webApp =
    choose [
        GET >=>
            choose [
                route "/logout" >=> mustBeAuthenticated Logout.handler
                route "/devices" >=> mustBeAuthenticated Devices.List.handler
                route "/organization" >=> mustBeAuthenticated Organization.Details.handler
            ]
        POST >=>
            choose [
                route "/login" >=> defaultBindJson Login.validatePayload Login.handler
                route "/register" >=> defaultBindJson Register.validatePayload Register.handler
                route "/devices" >=> mustBeAuthenticated (defaultBindJsonAndTransformWithArg Devices.Add.validatePayload Devices.Add.handler)
            ]
        DELETE >=>
            choose [
                routef "/devices/%s" (fun (macAddress: string) -> mustBeAuthenticated (Devices.Delete.handler macAddress))
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
    do customJsonSettings.Converters.Add(Json.DateOnlyJsonConverter() :> JsonConverter)
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
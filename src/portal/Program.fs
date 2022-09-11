module Gerlinde.Portal.Backend.App

open System
open System.IO
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

let jsonParsingError message =
    setStatusCode 400 >=> json message
    
let defaultBindJson<'a> = Json.tryBindJson<'a> jsonParsingError

let defaultBindJsonWithArg<'a, 'b> = Json.tryBindJsonWithExtra<'a, 'b> jsonParsingError

let webApp =
    choose [
        GET >=>
            choose [
                route "/login" >=> defaultBindJson Login.validatePayload Login.handler
            ]
        POST >=>
            choose [
                route "/register" >=> defaultBindJson Register.validatePayload Register.handler
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
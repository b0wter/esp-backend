namespace Gerlinde.Shared.Repository

open b0wter.CouchDb.Lib
open b0wter.CouchDb.Lib.DbProperties
open b0wter.CouchDb.Lib.Mango
open FsToolkit.ErrorHandling
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Repository

module CouchDb =
    
    type IDatabaseConfiguration =
        abstract member Host : string
        abstract member Port : int
        abstract member DeviceDatabaseName : string
        abstract member OrganizationDatabaseName : string
        abstract member Username : string
        abstract member Password : string
    
    type C (configuration: IDatabaseConfiguration) =
        let host = configuration.Host
        let port = configuration.Port
        let deviceDb = configuration.DeviceDatabaseName
        let organizationDb = configuration.OrganizationDatabaseName
        let user = configuration.Username
        let password = configuration.Password
        let credentials = Credentials.create(user, password)
        let dbProps = 
          do printfn
                 "Creating dbProps, host '%s', port '%i', device database '%s', organization database '%s', username '%s' and %s password"
                 host
                 port
                 deviceDb
                 organizationDb
                 credentials.Username
                 (if System.String.IsNullOrWhiteSpace(credentials.Password) then "without" else "with")
          match create(host, port, credentials, Http) with
          | Valid v -> v
          | HostIsEmpty -> failwith "The CouchDb parameter 'Host' is missing."
          | PortIsInvalid -> failwith "The CouchDb parameter 'Port' is invalid."
          
        // Caches the latest revisions for the devices retreieved from the database
        let deviceRevisionTable = System.Collections.Concurrent.ConcurrentDictionary<string, string>()

        let tryUpdateDeviceRevisionTable (id: string) (rev: string option) =
            match rev with
            | Some revision -> do deviceRevisionTable[id] <- revision
            | None -> ()
        
        let tryGetFromDeviceRevisionTable (entity: Device.DeviceEntity) =
            if deviceRevisionTable.ContainsKey entity.DatabaseId then Some deviceRevisionTable[entity.DatabaseId]
            else None
        
        let applyJsonSettings () =
            do Json.converters.Add(FifteenBelow.Json.OptionConverter() :> Newtonsoft.Json.JsonConverter)
            do Json.converters.Add(FifteenBelow.Json.UnionConverter() :> Newtonsoft.Json.JsonConverter)
        do applyJsonSettings()

        let authentication = (Server.Authenticate.queryAsResult dbProps) |> Async.RunSynchronously
        do printfn "Authentifaction: %A" authentication
        
        let createDbIfNotExisting dbName =
            asyncResult {
                let! exists = Databases.Exists.queryAsResult dbProps dbName
                if exists then
                    return Ok true
                else
                    let! _ = Databases.Create.queryAsResult dbProps dbName []
                    return Ok true
            }
            
        do createDbIfNotExisting deviceDb |> Async.RunSynchronously |> ignore
        do createDbIfNotExisting organizationDb |> Async.RunSynchronously |> ignore

        let currentConnectionStatus () =
            async {
                let! result = Server.Info.queryAsResult dbProps
                match result with
                | Ok o -> return sprintf "%A" o
                | Error e -> return e |> ErrorRequestResult.textAsString
            }
            
        let registerDevice dbProps organizationId (device: Device.Device) =
            asyncResult {
                let entity = device |> Device.toEntity organizationId
                let! result = Databases.AddDocument.queryAsResult dbProps deviceDb entity
                do deviceRevisionTable[result.Id] <- result.Rev
                return {| Id = result.Id; Rev = result.Rev|}
            } |> AsyncResult.mapError ErrorRequestResult.textAsString
            
        let findOrganizationByAccessToken (dbProps: DbProperties) (token: string) =
            asyncResult {
                let elementSelector = condition "token" (Equal <| Text token)
                let selector = combination <| ElementMatch (elementSelector, "accessTokens")
                let expression = selector |> createExpressionWithLimit 2
                let! result =
                    Databases.Find.queryAsResultWithOutput<Organization.OrganizationEntity> dbProps organizationDb expression
                    |> AsyncResult.mapError ErrorRequestResult.textAsString
                match result.Docs |> List.tryExactlyOne with
                | Some x -> return! Ok x
                | None -> return! Error "No organization could be found for the given organization access token"
            }
            
        let findDeviceByDeviceToken (dbProps: DbProperties) (token: string) =
            asyncResult {
                let expression = condition "accessToken" (Equal <| Text token) |> createExpressionWithLimit 2
                let! result =
                    Databases.Find.queryAsResult<Device.DeviceEntity> dbProps deviceDb expression
                    |> AsyncResult.mapError ErrorRequestResult.textAsString
                match result.Docs with
                | [] ->
                    return! Error "No device was found for the given device token"
                | [ head ] ->
                    do tryUpdateDeviceRevisionTable head.DatabaseId head.Revision
                    return! Ok result.Docs.Head
                | _ ->
                    return! Error "More than one device was found for the given device token"
            }
            
        let saveDevice (dbProps: DbProperties) (entity: Device.DeviceEntity) =
            Documents.Put.queryAsResult<Device.DeviceEntity> dbProps deviceDb (fun e -> e.DatabaseId) tryGetFromDeviceRevisionTable entity
            |> AsyncResult.mapError ErrorRequestResult.textAsString
            |> AsyncResult.map
                   (fun r ->
                        do deviceRevisionTable[r.Id] <- r.Rev
                        {| Id = r.Id; Rev = r.Rev |})
            
        member this.RegisterDevice organizationId device =
            registerDevice dbProps organizationId device
            
        member this.FindOrganizationByAccessToken token =
            findOrganizationByAccessToken dbProps token |> AsyncResult.map Organization.fromEntity
            
        member this.FindDeviceByAccessToken token =
            findDeviceByDeviceToken dbProps token |> AsyncResult.map Device.fromEntity
            
        member this.SaveDevice (device, organizationId) : Async<Result<{| Id: string; Rev: string |}, string>> =
            let entity = device |> (Device.toEntity organizationId)
            saveDevice dbProps entity
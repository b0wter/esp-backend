namespace Gerlinde.Shared.Repository

open System
open FsToolkit.ErrorHandling.Operator.AsyncResult
open b0wter.CouchDb.Lib
open b0wter.CouchDb.Lib.DbProperties
open b0wter.CouchDb.Lib.HttpVerbs.Head
open b0wter.CouchDb.Lib.Mango
open FsToolkit.ErrorHandling
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Repository
open b0wter.CouchDb.Lib.RequestResult

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
          
        // Caches the latest revisions for the devices/organizations retrieved from the database
        let deviceRevisionTable = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
        let organizationRevisionTable = System.Collections.Concurrent.ConcurrentDictionary<Guid, string>()

        let tryUpdateDeviceRevisionTable (id: string) (rev: string option) =
            match rev with
            | Some revision -> do deviceRevisionTable[id] <- revision
            | None -> ()
        
        let tryUpdateOrganizationRevisionTable (id: Guid) (rev: string option) =
            match rev with
            | Some revision -> do organizationRevisionTable[id] <- revision
            | None -> ()
        
        let tryGetFromDeviceRevisionTable (entity: Device.DeviceEntity) =
            if deviceRevisionTable.ContainsKey entity.DatabaseId then Some deviceRevisionTable[entity.DatabaseId]
            else None
        
        let tryGetFromOrganizationRevisionTable (entity: Organization.OrganizationEntity) =
            if organizationRevisionTable.ContainsKey entity.Id then Some organizationRevisionTable[entity.Id]
            else None
        
        let applyJsonSettings () =
            do Json.converters.Add(FifteenBelow.Json.OptionConverter() :> Newtonsoft.Json.JsonConverter)
            do Json.converters.Add(FifteenBelow.Json.UnionConverter() :> Newtonsoft.Json.JsonConverter)
            do Json.converters.Add(Json.DateOnlyJsonConverter() :> Newtonsoft.Json.JsonConverter)
        do applyJsonSettings()

        let authentication = (Server.Authenticate.queryAsResult dbProps) |> Async.RunSynchronously
        do printfn "Authentifaction: %A" authentication
        
        let createDbIfNotExisting dbName partitioned =
            asyncResult {
                let! exists = Databases.Exists.queryAsResult dbProps dbName
                if exists then
                    return Ok true
                else
                    let! _ = Databases.Create.queryAsResult dbProps dbName [ Databases.Create.QueryParameters.Partitioned partitioned ]
                    return Ok true
            }
            
        do createDbIfNotExisting deviceDb true |> Async.RunSynchronously |> ignore
        do createDbIfNotExisting organizationDb false |> Async.RunSynchronously |> ignore

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
                    Databases.Find.queryAsResult<Organization.OrganizationEntity> dbProps organizationDb expression
                    |> AsyncResult.mapError ErrorRequestResult.textAsString
                match result.Docs with
                | [] ->
                    return! Error "No organization was found for the given access token"
                | [ head ] ->
                    do tryUpdateOrganizationRevisionTable head.Id head.Revision
                    return! Ok head
                | _ ->
                    return! Error "More than one device was found for the given device token"
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
                    return! Ok head
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
                   
        let findOrganizationByEmail dbProps email =
            let expression = condition "email" (Equal <| Text email) |> createExpressionWithLimit 2
            Databases.Find.queryAsResult<Organization.OrganizationEntity> dbProps organizationDb expression
            |> AsyncResult.mapError ErrorRequestResult.textAsString
            |> Async.map (Result.bind
                (fun r ->
                    match r.Docs with
                    | [ ] ->  Error "Found no organization for the given email address"
                    | [ head ] ->
                        do tryUpdateOrganizationRevisionTable head.Id head.Revision
                        Ok head
                    | _ -> Error "Found multiple organizations for the given email laddress"
                ))
            
        let saveOrganization dbProps entity =
            Documents.Put.queryAsResult<Organization.OrganizationEntity> dbProps organizationDb (fun e -> e.Id |> string) tryGetFromOrganizationRevisionTable entity
            |> AsyncResult.mapError ErrorRequestResult.textAsString
            |> AsyncResult.map
                (fun r ->
                    do organizationRevisionTable[Guid.Parse(r.Id)] <- r.Rev
                    {| Id = r.Id; Rev = r.Rev |})
                
        let getDevicesForOrganization dbProps organizationId =
            let queryParameters = { Partitions.AllDocs.EmptyQueryParameters with IncludeDocs = Some true }
            Partitions.AllDocs.queryWithAsResult<string, Device.DeviceEntity> dbProps deviceDb (organizationId |> string) queryParameters
            |> AsyncResult.mapError ErrorRequestResult.textAsString
            |> AsyncResult.map
                (fun r ->
                    let docs =
                       r.Rows
                       |> List.map (fun row -> row.Doc)
                       |> List.choose id
                    do docs
                       |> List.iter (fun doc -> tryUpdateDeviceRevisionTable doc.DatabaseId doc.Revision)
                    docs)
            
        let deleteDeviceForOrganization dbProps (organizationId: Guid) macAddress =
            asyncResult {
                let deviceId = $"%O{organizationId}:%s{macAddress}"
                let! revision =
                    if deviceId |> deviceRevisionTable.ContainsKey then
                        asyncResult { return deviceRevisionTable[deviceId] }
                    else
                        asyncResult {
                            let! response = Documents.Head.queryAsResult dbProps deviceDb deviceId
                            let revision = response.ETag.TrimEnd('"').TrimStart('"')
                            return revision
                        }
                return! Documents.Delete.queryAsResult dbProps deviceDb $"%O{organizationId}:%s{macAddress}" revision
            }
            |> AsyncResult.mapError (fun err -> err |> ErrorRequestResult.textAsString)
            |> AsyncResult.map (fun _ ->
                    do deviceRevisionTable.TryRemove $"%O{organizationId}:%s{macAddress}" |> ignore
                )
            
        let findDeviceInOrganization dbProps organizationId macAddress =
            asyncResult {
                let id = $"%O{organizationId}:%s{macAddress}"
                let matchId = condition "_id" (Equal <| Text id) |> createExpression
                let! response =
                    Databases.Find.queryAsResult<Device.DeviceEntity> dbProps deviceDb matchId
                    |> AsyncResult.mapError (fun err -> err |> ErrorRequestResult.textAsString)
                    
                match response.Docs with
                | [] ->
                    return! Error $"Could not find any devices with the id %s{id}"
                | [ single ] ->
                    do tryUpdateDeviceRevisionTable single.DatabaseId single.Revision
                    return! Ok single
                | many ->
                    return! Error $"A search for a device by id returned %i{many.Length} results"
            }
        
        let doesDeviceExist dbProps organizationId macAddress =
            asyncResult {
                let id = $"%O{organizationId}:%s{macAddress}"
                let! response = Documents.Head.query dbProps deviceDb id
                match response with
                | HttpVerbs.Head.Result.NotModified _
                | HttpVerbs.Head.Result.DocumentExists _ ->
                    return! Ok true
                | HttpVerbs.Head.Result.NotFound _ ->
                    return! Ok false
                | Unauthorized e
                | DbNameMissing e
                | ParameterIsMissing e
                | DocumentIdMissing e
                | Unknown e ->
                    return! Error e.Content
            }
            
        member this.RegisterDevice organizationId device =
            registerDevice dbProps organizationId device
            
        member this.FindOrganizationByAccessToken token =
            findOrganizationByAccessToken dbProps token |> AsyncResult.map Organization.fromEntity
            
        member this.FindDeviceByAccessToken token =
            findDeviceByDeviceToken dbProps token |> AsyncResult.map Device.fromEntity
            
        member this.SaveDevice (device, organizationId) : Async<Result<{| Id: string; Rev: string |}, string>> =
            let entity = device |> (Device.toEntity organizationId)
            saveDevice dbProps entity
            
        member this.FindOrganizationByEmail email =
            findOrganizationByEmail dbProps email |> AsyncResult.map Organization.fromEntity
            
        member this.SaveOrganization organization : Async<Result<{| Id: string; Rev: string |}, string>> =
            let entity = organization |> Organization.toEntity
            saveOrganization dbProps entity
            
        member this.GetDevicesForOrganization organizationId : Async<Result<Device.Device list, string>> =
            organizationId |> getDevicesForOrganization dbProps |> AsyncResult.map (List.map (Device.fromEntity >> fst))
            
        member this.DeleteDeviceForOrganization organizationId macAddress : Async<Result<unit, string>> =
            macAddress |> (deleteDeviceForOrganization dbProps organizationId)
            
        member this.FindDeviceInOrganization organizationId macAddress : Async<Result<Device.Device * Guid, string>> =
            macAddress
            |> (findDeviceInOrganization dbProps organizationId)
            |> AsyncResult.map Device.fromEntity
            
        member this.DoesDeviceExist organizationId macAddress : Async<Result<bool, string>> =
            doesDeviceExist dbProps organizationId macAddress
namespace Gerlinde.Portal.Backend.Devices

open System
open Gerlinde.Portal.Backend.Login
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Lib.Device
open Gerlinde.Shared.WebApi
open FsToolkit.ErrorHandling
open Gerlinde.Shared.Repository
open Microsoft.AspNetCore.Http
open Giraffe

module List =
    let handler (organization: Organization.Organization, _) =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                let! devices = repo.GetDevicesForOrganization organization.Id
                return! ctx.WriteJsonAsync devices   
            } |> Handler.mapErrorToResponse ctx

module Add =
    [<CLIMutable>]
    type Payload = {
        MacAddress: string
        Name: string option
        Description: string option
        CurrentFirmwareVersion: string option
        AvailableFirmwareVersions : Firmware list option
        KnownCommands: KnownCommand list option
    }

    let validatePayload (payload: Payload) =
        let mustNotBeEmpty (fieldName: string) (s: string) =
            if String.IsNullOrWhiteSpace s then Validation.error $"%s{fieldName} must not be empty"
            else Validation.ok s
            
        let mustNotExceed (length: int) fieldName (s: string) =
            if s.Length <= length then Validation.ok s
            else Validation.error (sprintf $"%s{fieldName} must not exceed %i{length} characters")
            
        let mayNotExceed (length: int) fieldName (option: string option) =
            match option with
            | Some s when s.Length <= length -> Validation.ok option
            | Some _ -> Validation.error (sprintf $"%s{fieldName} must not exceed %i{length} characters")
            | None -> Validation.ok None
    
        let validateFirmwareVersions (versions: Firmware list) =
            let validateFirmware (firmware: Firmware) =
                validation {
                    let! _ = firmware.Hash |> mayNotExceed 1024 "hash"
                    and! _ = firmware.Url |> mustNotExceed 4096 "url"
                    and! _ = firmware.Version |> mustNotExceed 4096 "version"
                    return firmware
                }
            validation {
                let! _ = versions |> List.map validateFirmware |> List.sequenceValidationA
                return versions
            }
            
        let validateKnownCommands (commands: KnownCommand list) =
            let validateCommand (command: KnownCommand) =
                let validateName (c: KnownCommand) =
                    if c.Name |> String.length <= 4096 then Validation.ok c
                    else Validation.error "Command names must not be longer than 4096 characters"
                let validateDesc (c: KnownCommand) =
                    if c.Description |> String.length <= 4096 then Validation.ok c
                    else Validation.error "Command description must not be longer than 4096 characters"
                validation {
                    let! _ = command |> validateName
                    and! _ = command |> validateDesc
                    return command
                }
            
            validation {
                let! _ = commands |> List.map validateCommand |> List.sequenceValidationA
                return commands
            }

        validation {
            let! validatedMacAddress =
                payload.MacAddress
                |> mustNotBeEmpty "macAddress"
                |> Validation.bind (mustNotExceed 128 "macAddress")
            let! validatedName = payload.Name |> mayNotExceed 1024 "name"
            and! validatedDescription = payload.Description |> mayNotExceed 2048 "description"
            and! validatedFirmware = payload.CurrentFirmwareVersion |> mayNotExceed 1024 "firmware"
            and! validatedFirmwareVersions =
                payload.AvailableFirmwareVersions
                |> Option.defaultValue []
                |> validateFirmwareVersions
            and! validatedCommands =
                payload.KnownCommands
                |> Option.defaultValue []
                |> validateKnownCommands
            let device = {
                AccessToken = String.Empty
                Description = validatedDescription
                CurrentFirmwareVersion = validatedFirmware
                AvailableFirmwareVersions = validatedFirmwareVersions
                KnownCommands = validatedCommands
                OutstandingCommands = []
                FinishedCommands = []
                MacAddress = validatedMacAddress
                Name = validatedName
                StatusUpdates = []
            }
            return device
        }
    
    let handler (payload: Validation<Device.Device, string>) (organization: Organization.Organization, _: string) =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                match payload with
                | Validation.Ok device ->
                    let! exists = repo.DoesDeviceExist organization.Id device.MacAddress
                    if exists then
                        do ctx.SetStatusCode 500
                        return! ctx.WriteStringAsync "A device with the same mac address already exists for this organization"
                    else
                        let device = { device with AccessToken = Utilities.generateToken 64 }
                        let! device = repo.SaveDevice (device, organization.Id)
                        return! ctx.WriteJsonAsync device
                | Validation.Error errors ->
                    do ctx.SetStatusCode 400
                    let merged = String.Join(Environment.NewLine, errors)
                    return! ctx.WriteStringAsync merged
            } |> Handler.mapErrorToResponse ctx
            
module Delete =
    let handler (macAddress: string) (organization: Organization.Organization, _) =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                let! _ = macAddress |> repo.DeleteDeviceForOrganization organization.Id
                do ctx.SetStatusCode 204
                return! next ctx
            } |> Handler.mapErrorToResponse ctx
            
module Details =
    let handler (macAddress: string) (organization: Organization.Organization, _) =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                let! device, _ = macAddress |> repo.FindDeviceInOrganization organization.Id
                do ctx.SetStatusCode 200
                return! ctx.WriteJsonAsync device
            } |> Handler.mapErrorToResponse ctx

namespace Gerlinde.Shared.Repository

open System
open Gerlinde.Shared.Lib.Device
open Newtonsoft.Json

module Device =

    /// <summary>
    /// Representation of a device in the persistence layer
    /// </summary>
    type DeviceEntity =
        {
        AccessToken: string
        AvailableFirmwareVersions: Firmware list option
        CurrentFirmwareVersion: string option
        Description: string option
        FinishedCommands: FinishedCommand list option
        KnownCommands: KnownCommand list option
        MacAddress: string
        Name: string option
        OrganisationId: Guid
        OutstandingCommands: OutstandingCommand list option
        [<JsonProperty("_rev")>]
        Revision: string option
        StatusUpdates: StatusUpdate list option
        }
        member this.``type`` = "Device"
        [<JsonProperty("_id")>]
        member this.DatabaseId = this.OrganisationId.ToString() + ":" + this.MacAddress

    /// <summary>
    /// Transforms a device into an entity that can be persisted into the database.
    /// The `organizationId` is required for the database partition key "$organizationId:$macAddress"
    /// </summary>
    let toEntity (organizationId: Guid) (device: Device) : DeviceEntity =
        {
            AccessToken = device.AccessToken
            Description = device.Description
            CurrentFirmwareVersion = device.CurrentFirmwareVersion
            AvailableFirmwareVersions = Some device.AvailableFirmwareVersions
            OutstandingCommands = Some device.OutstandingCommands
            FinishedCommands = Some device.FinishedCommands
            KnownCommands = Some device.KnownCommands
            MacAddress = device.MacAddress
            Name = device.Name
            OrganisationId = organizationId
            Revision = None // the revision is set by the database repository
            StatusUpdates = Some device.StatusUpdates
        }
        
    /// <summary>
    /// Transforms a device entity into a device and a organization id.
    /// </summary>
    let fromEntity (entity: DeviceEntity) =
        let inline makeEmptyIfNone (list: 'a list option) : 'a list =
            list |> Option.defaultValue []
        ({
            Device.AccessToken = entity.AccessToken
            Device.Description = entity.Description
            Device.CurrentFirmwareVersion = entity.CurrentFirmwareVersion
            Device.AvailableFirmwareVersions = entity.AvailableFirmwareVersions |> makeEmptyIfNone
            Device.OutstandingCommands = entity.OutstandingCommands |> makeEmptyIfNone
            Device.FinishedCommands = entity.FinishedCommands |> makeEmptyIfNone
            Device.KnownCommands = entity.KnownCommands |> makeEmptyIfNone
            Device.MacAddress = entity.MacAddress
            Device.Name = entity.Name
            Device.StatusUpdates = entity.StatusUpdates |> makeEmptyIfNone
        }
        , entity.OrganisationId)
           
namespace Gerlinde.Esp.Backend

module Device =
    open System
    open System.Dynamic
    open Newtonsoft.Json
    open FsToolkit.ErrorHandling
    
    type RemoteCommand = {
        /// <summary>
        /// The id can be used to report to the backend that this command was executed.
        /// </summary>
        Id: Guid
        /// <summary>
        /// Name of the command that shall be executed.
        /// </summary>
        Name: string
    }

    /// <summary>
    /// Result of the execution of a command
    /// </summary>
    [<CLIMutable>]
    type RemoteCommandResult = {
        /// <summary>
        /// Id of the command that was executed.
        /// </summary>
        Id: Guid
        Success: bool
        Details: string option
    }
    
    /// <summary>
    /// Command that an ESP can execute. The name has to be unique and is required to send commands to the device
    /// </summary>
    type KnownCommand = {
        Name: string
        Description: string
    }
    
    /// <summary>
    /// Command that a device is requested to do
    /// </summary>
    type OutstandingCommand = {
        Id: Guid
        RequestedAt: DateTime
        Command: string
    }
    
    type FinishedCommand = {
        Id: Guid
        Name: string
        // May be none because a device reports an unknown command as finished.
        RequestedAt: DateTime option
        FinishedAt: DateTime
        IsSuccess: bool
        Details: string option
        /// <summary>
        /// Marks whether the backend knew the id of the finished command.<br/>
        /// If a device reports that it finished a command with an id that is unknown to the backend the result is
        /// still persisted but `WasKnownRequest` is set to false.
        /// </summary>
        WasKnownRequest: bool
    }
    
    type Firmware = {
        Version : string
        Timestamp: DateTime
        Hash: string option
        Url: string
    }

    /// <summary>
    /// Registration request for a new device.
    /// </summary>
    [<CLIMutable>]
    type DeviceRegistrationRequest = {
        Description: string
        MacAddress: string
        OrganisationAccessToken: string
        FirmwareVersion: string
        Name: string
        KnownCommands: KnownCommand list option
    }
    
    /// <summary>
    /// Updates the firmware version and known commands for a device
    /// </summary>
    [<CLIMutable>]
    type DeviceUpdate = {
        FirmwareVersion: string
        KnownCommands: KnownCommand list option
    }
        
    /// <summary>
    /// The registration result is a device access token
    /// </summary>
    type DeviceRegistrationResult = string
    
    /// <summary>
    /// Timestamped status update from a device. The status itself may be any valid json.
    /// </summary>
    type StatusUpdate = {
        Timestamp: DateTime
        Status: ExpandoObject
    }
    
    /// <summary>
    /// A device representation in the backend logic
    /// </summary>
    type Device = {
        AccessToken: string
        Description: string option
        /// <summary>
        /// Current firmware is a string because it is possible for a device to report a firmware version that is
        /// unknown. It's an option because some devices might not even report a firmware version at all.
        /// </summary>
        CurrentFirmwareVersion: string option
        AvailableFirmwareVersions: Firmware list
        KnownCommands: KnownCommand list
        OutstandingCommands: OutstandingCommand list
        FinishedCommands: FinishedCommand list
        MacAddress: string
        Name: string option
        StatusUpdates: StatusUpdate list
    }

    let latestAvailableFirmware (d: Device) : Firmware option =
        d.AvailableFirmwareVersions
        |> List.sortByDescending (fun f -> f.Timestamp)
        |> List.tryHead

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
    /// Creates a new device instance. `address` and `token` are required, every other parameter is optional.<br/>
    /// The new device does not have any information set for the current firmware, outstanding/finished/known commands as
    /// well as status updates.
    /// </summary>
    let create address token description firmware commands name =
        {
            AccessToken = token
            Description = description
            CurrentFirmwareVersion = firmware
            AvailableFirmwareVersions = []
            KnownCommands = commands
            OutstandingCommands = []
            FinishedCommands = []
            MacAddress = address
            Name = name
            StatusUpdates = []
        }
        
    /// <summary>
    /// Creates a device from a device registration. The registration is not checked for validity.
    /// Requires the caller to supply a device authentication token.
    /// </summary>
    let fromRegistration (registration: DeviceRegistrationRequest) deviceAuthentificationToken : Device =
        let description =
            if registration.Description |> String.IsNullOrWhiteSpace then None
            else Some registration.Description
            
        let firmware =
            if registration.FirmwareVersion |> String.IsNullOrWhiteSpace then None
            else Some registration.FirmwareVersion
                
        let commands =
            registration.KnownCommands
            |> Option.defaultValue []
            |> List.map (fun c -> { Name = c.Name; Description = c.Description })
            
        let name =
            if registration.Name |> String.IsNullOrWhiteSpace then None
            else Some registration.Name
            
        create registration.MacAddress deviceAuthentificationToken description firmware commands name
        
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
           
namespace Gerlinde.Shared.Lib

open System.Dynamic

module Device =
    open System
    
    type DeviceIdentification = {
        MacAddress: string
        OrganisationId: Guid
    }
    
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

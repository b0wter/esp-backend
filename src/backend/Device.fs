namespace Gerlinde.Esp.Backend

module Device =
    open System
    open Newtonsoft.Json
    open FsToolkit.ErrorHandling
    open Gerlinde.Shared.Lib.Device

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
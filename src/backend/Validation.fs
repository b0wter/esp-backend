namespace Gerlinde.Esp.Backend

open System.Dynamic
open Gerlinde.Esp.Backend.Device
open FsToolkit.ErrorHandling
open System
open Gerlinde.Shared.Lib.Device

module Validation =

    let private validateCommands (commandList: KnownCommand list option) : Validation<KnownCommand list option, string > =
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
            let commands = commandList |> Option.defaultValue []
            let! _ = commands |> List.map validateCommand |> List.sequenceValidationA
            return commandList
        }

    /// <summary>
    /// Returns a validated `DeviceRegistrationRequest` or an error message as string
    /// </summary>
    let deviceRegistrationRequest (d: DeviceRegistrationRequest) =
        let validateDescription d =
            if d |> String.length <= 4096 then Ok d
            else Validation.error "Description cannot be longer than 4096 characters"
        let validateMacAddressLength m =
            if m |> String.length = 12 then Validation.ok m
            else Validation.error "Mac Address needs to be 12 characters long"
        let validateMacAddressFormat m =
            if System.Text.RegularExpressions.Regex.IsMatch(m, @"\A\b[0-9a-fA-F]+\b\Z") then Validation.ok m
            else Validation.error "Mac Address needs to be in hexadecimal format"
        let validateOat o =
            if o |> String.IsNullOrWhiteSpace then Validation.error "Organisation Access Token must not be empty"
            else Validation.ok o
        let validateFirmware f =
            if f |> String.length <= 4096 then Ok d.FirmwareVersion
            else Validation.error "Firmware version cannot be longer than 4096 characters"
        validation {
            let! validDescription = validateDescription d.Description
            and! validMacAddress = d.MacAddress |> (validateMacAddressLength >> Validation.bind validateMacAddressFormat)
            and! validOat = validateOat d.OrganisationAccessToken
            and! validCommands = validateCommands d.KnownCommands
            and! validFirmware = validateFirmware d.FirmwareVersion
            return {
                d with
                    Description = validDescription
                    MacAddress = validMacAddress
                    OrganisationAccessToken = validOat
                    KnownCommands = validCommands
                    FirmwareVersion = validFirmware
            }
        }
    
    let markCommandsFinished (commands: RemoteCommandResult list) =
        let validate (details: string option) =
            details
            |> Option.map (fun d ->
                if d.Length <= 4096 then Validation.ok details
                else Validation.error "Remote command result details must not be longer than 4096 characters")
            |> Option.defaultValue (Validation.ok None)
        validation {
            let! _ = commands |> List.map (fun c -> validate c.Details) |> List.sequenceValidationA
            return commands
        }

    /// <summary>
    /// Validates an incoming device update
    /// </summary>
    let updateDevice (update: DeviceUpdate) =
        let validateFirmwareVersion f =
            if f |> String.length <= 1024 then Ok f
            else Validation.error "Firmware version must not be longer than 1024 characters"
        validation {
            let! validFirmware = validateFirmwareVersion update.FirmwareVersion
            and! validCommands = validateCommands update.KnownCommands
            return {
                update with
                    FirmwareVersion = validFirmware
                    KnownCommands = validCommands
            }
        }
    
    /// <summary>
    /// Validates an incoming status update. Since the status update can be anz valid json there is nothing to do here
    /// </summary>
    let statusUpdate (update: ExpandoObject) =
        Validation.ok update

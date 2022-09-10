namespace Gerlinde.Portal.Cli

module Arguments =
    open Argu
    open System
    
    type AddOrganizationArgs =
        | [<ExactlyOnce>] Name of string
        | [<ExactlyOnce>] Email of string
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Name _ -> "Name of the organization"
                | Email _ -> "Email address of the organization"

    type OrganizationArgs =
        | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddOrganizationArgs>
    
    type AddDeviceArgs =
        | [<MainCommand>] MacAddress of string
        | [<Unique>] Name of string
        | [<Unique>] Description of string
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | MacAddress _ -> "Mac address of the given device, must be supplied and unique for the given organization"
                | Name _ -> "Name of the device"
                | Description _ -> "Additional device description"
    
    type DeviceArgs =
        | [<CliPrefix(CliPrefix.None); First>] Add of ParseResults<AddDeviceArgs>
        | [<CliPrefix(CliPrefix.None); First>] Delete of string
        | [<CliPrefix(CliPrefix.None); First>] List
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Add _ -> "Add a new device"
                | Delete _ -> "Delete a device by it's mac address"
                | List _ -> "Lists all devices"

    type MainArgs =
        | [<CliPrefix(CliPrefix.None)>] Login
        | [<CliPrefix(CliPrefix.None)>] Logout
        | [<CliPrefix(CliPrefix.None)>] Device of ParseResults<DeviceArgs>
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Device _ -> "Add/delete/update device data"
                | Login _ -> "Logs into a device backend"
                | Logout _ -> "Delets all local login details"

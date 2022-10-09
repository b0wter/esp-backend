namespace Gerlinde.Portal.Cli

module Arguments =
    open Argu
    
    type LoginArgs =
        | [<ExactlyOnce>] Email of string
        | [<ExactlyOnce>] Password of string
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Email _ -> "Email address to login with, if not supplied will be read from stdin"
                | Password _ -> "Password to login with, if not supplied will be read from stdin"
    
    type AddOrganizationArgs =
        | [<Unique>] Name of string
        | [<Unique>] Email of string
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Name _ -> "Name of the organization"
                | Email _ -> "Email address of the organization"

    type OrganizationArgs =
        | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddOrganizationArgs>
        | [<CliPrefix(CliPrefix.None)>] Show
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Add _ -> "Registers a new organization"
                | Show -> "Show the details of the current organization"
    
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
        | [<CliPrefix(CliPrefix.None)>] Login of ParseResults<LoginArgs> 
        | [<CliPrefix(CliPrefix.None)>] Logout
        | [<CliPrefix(CliPrefix.None)>] Status
        | [<CliPrefix(CliPrefix.None)>] Device of ParseResults<DeviceArgs>
        | [<CliPrefix(CliPrefix.None)>] Organization of ParseResults<OrganizationArgs>
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Device _ -> "Add/delete/update device data"
                | Organization _ -> "Add/update/show (current) organizations"
                | Login _ -> "Logs into a device backend"
                | Logout _ -> "Deletes all local login details"
                | Status _ -> "Lists various current status infos"

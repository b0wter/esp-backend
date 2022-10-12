namespace Gerlinde.Portal.Cli

open Argu
open Gerlinde.Portal.Cli.Arguments
open Gerlinde.Shared.Lib
open Gerlinde.Shared.Lib.Organization

module Config =

    type AddOrganizationConfig =
        { Email: string option
          Name: string option
          Password: string option }
    let private EmptyAddOrganizationConfig = { Email = None; Name = None; Password = None }

    type AddDeviceConfig =
        { MacAddress: string
          Name: string option
          Description: string option }
    let private EmptyAddDeviceConfig = { MacAddress = System.String.Empty; Name = None; Description = None }

    type LoginConfig =
        { Email: string option
          Password: string option }
    let private EmptyLoginConfig = { Email = None; Password = None }

    type Command =
        // --------------------------
        | AddOrganization of AddOrganizationConfig
        | OrganizationDetails
        // --------------------------
        | AddDevice of AddDeviceConfig
        | DeleteDevice of macAddress:string
        | ListDevices
        | DeviceDetails of macAddress:string
        // --------------------------
        | Login of LoginConfig
        | Logout
        | Status
        // --------------------------
        | Uninitialized
        | InvalidAuthToken of AccessToken

    type Config =
        {
            Command: Command
            Verbose: bool
            AccessToken: Organization.AccessToken option
            Host: string
        }

    let EmptyConfig =
        {
            Command = Uninitialized
            Verbose = false
            AccessToken = None
            Host = System.String.Empty
        }

    let applyLoginConfig (config: LoginConfig) (l: LoginArgs) : LoginConfig =
        match l with
        | LoginArgs.Email e -> { config with Email = Some e }
        | LoginArgs.Password p -> { config with Password = Some p }
    
    let applyAddOrganizationConfig (config: AddOrganizationConfig) (a: AddOrganizationArgs) : AddOrganizationConfig =
        match a with
        | AddOrganizationArgs.Name n -> { config with Name = Some n }
        | AddOrganizationArgs.Email e -> { config with Email = Some e }
        | AddOrganizationArgs.Password p -> { config with Password = Some p }

    let rec applyOrganizationArg (config: Config) (o: OrganizationArgs) : Config =
        match o with
        | OrganizationArgs.Add args ->
            let c =
                match config.Command with
                | Command.AddOrganization x -> x
                | _ -> EmptyAddOrganizationConfig

            { config with
                Command =
                    Command.AddOrganization(
                        args.GetAllResults ()
                        |> List.fold applyAddOrganizationConfig c
                    ) }
        | OrganizationArgs.Details -> { config with Command = OrganizationDetails }
        
    let rec applyDeviceArg (config: Config) (d: DeviceArgs) : Config =
        let applyAddDeviceArg (c: AddDeviceConfig) (a: AddDeviceArgs) =
            match a with
            | AddDeviceArgs.Name n -> { c with Name = Some n }
            | AddDeviceArgs.Description d -> { c with Description = Some d }
            | AddDeviceArgs.MacAddress m -> { c with MacAddress = m }
        
        match d with
        | DeviceArgs.Add args ->
            let c =
                match config.Command with
                | Command.AddDevice x -> x
                | _ -> EmptyAddDeviceConfig
            { config with
                Command =
                    Command.AddDevice(
                        args.GetAllResults ()
                        |> List.fold applyAddDeviceArg c
                    ) }
        | DeviceArgs.Delete macAddress ->
            { config with Command = Command.DeleteDevice macAddress }
        | DeviceArgs.List ->
            { config with Command = Command.ListDevices }
        | DeviceArgs.Details macAddress ->
            { config with Command = Command.DeviceDetails macAddress }

    let applyMainArg (config: Config) (m: MainArgs) : Config =
        match m with
        | MainArgs.Host host ->
            { config with Host = host }
        | MainArgs.Login args ->
            let c =
                match config.Command with
                | Command.Login x -> x
                | _ -> EmptyLoginConfig
 
            { config with
                Command =
                    Command.Login(
                        args.GetAllResults()
                        |> List.fold applyLoginConfig c
                    ) }
        | MainArgs.Logout -> { config with Command = Logout }
        | MainArgs.Status -> { config with Command = Status }
        | MainArgs.Device args ->
            args.GetAllResults()
            |> List.fold applyDeviceArg config
        | MainArgs.Organization args ->
            args.GetAllResults()
            |> List.fold applyOrganizationArg config

    let applyAllArgs (results: ParseResults<MainArgs>) =
        let args = results.GetAllResults ()
        args |> List.fold applyMainArg EmptyConfig
namespace Gerlinde.Esp.Backend

open Newtonsoft.Json

module Organization =
    open System
    
    type AccessToken =
        {
            Token: string
            Name: string option
            ValidThrough: DateOnly
        }
    
    type Organization =
        {
            Id: Guid
            Email: string
            PasswordHash: string
            Name: string
            Devices: Device.Device list
            /// <summary>
            /// Access tokens generated for this organization. Not to be confused with the device access tokens
            /// </summary>
            AccessTokens: AccessToken list
        }

    type OrganizationEntity =
        {
            [<JsonProperty("_id")>]
            Id: Guid
            Email: string
            PasswordHash: string
            Name: string
            Devices: Device.DeviceEntity list
            /// <summary>
            /// Access tokens generated for this organization. Not to be confused with the device access tokens
            /// </summary>
            AccessTokens: AccessToken list
        }
        member this.``type`` = "Organization"

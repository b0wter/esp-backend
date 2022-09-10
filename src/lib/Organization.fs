namespace Gerlinde.Shared.Lib

open System

module Organization =
    
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
    
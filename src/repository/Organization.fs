namespace Gerlinde.Shared.Repository

open System
open Gerlinde.Shared.Lib.Organization
open Newtonsoft.Json

module Organization =

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

    let fromEntity (entity: OrganizationEntity) : Organization =
        {
            Organization.Devices = entity.Devices |> List.map (Device.fromEntity >> fst)
            Organization.Email = entity.Email
            Organization.Id = entity.Id
            Organization.Name = entity.Name
            Organization.AccessTokens = entity.AccessTokens
            Organization.PasswordHash = entity.PasswordHash
        }
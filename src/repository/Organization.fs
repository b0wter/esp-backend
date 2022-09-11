namespace Gerlinde.Shared.Repository

open System
open Gerlinde.Shared.Authentication
open Gerlinde.Shared.Lib.Organization
open Newtonsoft.Json

module Organization =

    type OrganizationEntity =
        {
            [<JsonProperty("_id")>]
            Id: Guid
            [<JsonProperty("_rev")>]
            Revision: string option
            Email: string
            PasswordHash: string
            Salt: string
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
            Organization.PasswordHash = (Hashing.Hash entity.PasswordHash)
            Organization.Salt = (Hashing.Base64Salt entity.Salt)
        }
        
    let toEntity (org: Organization) : OrganizationEntity =
        {
            Id = org.Id
            Revision = None
            Email = org.Email
            PasswordHash =  org.PasswordHash |> Hashing.hashAsString
            Salt = org.Salt |> Hashing.base64SaltAsString
            Name = org.Name
            Devices = org.Devices |> List.map (Device.toEntity org.Id)
            AccessTokens = org.AccessTokens
        }
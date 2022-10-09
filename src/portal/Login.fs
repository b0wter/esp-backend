namespace Gerlinde.Portal.Backend

open System
open Gerlinde.Shared.Authentication
open Gerlinde.Shared.Repository
open Gerlinde.Shared.Lib
open Giraffe
open Microsoft.AspNetCore.Http
open FsToolkit.ErrorHandling
open Gerlinde.Shared.WebApi

module Login =

    [<CLIMutable>]
    type Payload = {
        Email: string
        Password: string
    }
    
    let isMatchingPassword hashedPassword (base64Salt: Hashing.Base64Salt) password =
        let salt = Convert.FromBase64String(base64Salt |> Hashing.base64SaltAsString) |> Hashing.Salt
        let password = Password.Password password
        hashedPassword = (Hashing.hash password salt)
    
    let validatePayload (payload: Payload) =
        let mustNotBeEmpty (s: string) =
            if String.IsNullOrWhiteSpace s then Validation.error "Username and password must not be empty"
            else if s.Length > 256 then Validation.error "Username and password should not be more than 256 characters"
            else Validation.ok s
        validation {
            let! _ = payload.Email |> mustNotBeEmpty
            and! _ = payload.Password |> mustNotBeEmpty
            return payload
        }
    
    let handler (payload: Payload) : HttpHandler =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                let! organization = repo.FindOrganizationByEmail payload.Email
                if payload.Password |> isMatchingPassword organization.PasswordHash organization.Salt then
                    let token = {
                        Organization.AccessToken.Token = Utilities.generateToken 64
                        Organization.AccessToken.Name = Some (sprintf "Login from %s at %A" (ctx.Connection.RemoteIpAddress.ToString()) DateTime.Now)
                        Organization.AccessToken.ValidThrough = DateTime.Now.AddMonths(2) |> DateOnly.FromDateTime
                    }
                    let updatedOrganization = { organization with AccessTokens = token :: organization.AccessTokens }
                    let! _ = repo.SaveOrganization updatedOrganization
                    return! ctx.WriteJsonAsync {| Token = token.Token; ValidThrough = token.ValidThrough |}
                else
                    do ctx.SetStatusCode 401
                    return! ctx.WriteStringAsync "The given credentials are not valid"
            } |> Handler.mapErrorToResponse ctx


namespace Gerlinde.Portal.Backend

open System
open Gerlinde.Shared.Authentication
open Microsoft.AspNetCore.Http
open FsToolkit.ErrorHandling
open Giraffe
open Gerlinde.Shared.WebApi
open Gerlinde.Shared.Repository
open Gerlinde.Shared.Lib

module Register =
    
    [<CLIMutable>]
    type Registration = {
        Name: string
        Email: string
        Password: string
    }
    
    let validatePayload (registration: Registration) =
        let validateName (s: string) =
            if String.IsNullOrWhiteSpace(s) then Validation.error "Name must not be empty"
            else if s.Length > 256 then Validation.error "Name must not exceed 255 characters"
            else Validation.ok s
        let validateEmail (s: string) =
            if s |> Utilities.isValidEmail then Validation.ok s
            else Validation.error "The email address is invalid"
        let validatePassword (s: string) =
            if String.IsNullOrWhiteSpace(s) then Validation.error "Password must not be empty"
            else
                let predicates = [
                    (fun chars -> chars |> Array.length >= 12), "Password must not be less than 12 characters"
                    (fun chars -> chars |> Array.exists Char.IsDigit), "Password does not contain digit"
                    (fun chars -> chars |> Array.exists Char.IsUpper), "Password does not contain upper case letter"
                    (fun chars -> chars |> Array.exists Char.IsLower), "Password does not cntain lower case letter"
                    (fun chars -> chars |> Array.exists Char.IsSymbol), "Password does not contain symbol"
                ]
                let chars = s.ToCharArray()
                
                let failedPredicates =
                    predicates
                    |> List.map (fun (predicate: char[] -> bool, explanation: string) -> if chars |> (not << predicate) then Some explanation else None)
                    |> List.choose id
                
                if failedPredicates |> List.isEmpty then Validation.ok s
                else Validation.error (String.Join("; ", failedPredicates))
        validation {
            let! _ = registration.Email |> validateEmail
            and! _ = registration.Password |> validatePassword
            and! _ = registration.Name |> validateName
            return registration
        }

    let handler (payload: Registration) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                let organization = Organization.create payload.Email payload.Password payload.Name
                let accessToken = Organization.createToken (Some "organization registration")
                let organizationWithToken = { organization with AccessTokens = [ accessToken ] }
                let! _ = repo.SaveOrganization organizationWithToken
                do ctx.SetStatusCode 201
                return! ctx.WriteStringAsync accessToken.Token
            } |> Handler.mapErrorToResponse ctx

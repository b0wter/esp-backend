namespace Gerlinde.Portal.Backend

open FsToolkit.ErrorHandling
open Gerlinde.Shared.Repository
open Gerlinde.Shared.WebApi
open Giraffe
open Gerlinde.Shared.Lib
open Microsoft.AspNetCore.Http

module Logout =
    
    let handler (organization: Organization.Organization, token: string) =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            asyncResult {
                if organization.AccessTokens |> List.exists (fun at -> at.Token = token) then
                    let updatedOrg = { organization with AccessTokens = organization.AccessTokens |> List.where (fun at -> at.Token <> token) }
                    let repo = ctx.GetService<CouchDb.C>()
                    let! _ = repo.SaveOrganization updatedOrg
                    do ctx.SetStatusCode 204
                    return! ctx.WriteStringAsync System.String.Empty
                else
                    do ctx.SetStatusCode 500
                    return! ctx.WriteStringAsync "Internal server error: the given token could be used to login but is unknown to the organization"
            } |> Async.StartAsTask |> Handler.mapErrorToResponse ctx


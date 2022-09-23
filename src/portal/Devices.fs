namespace Gerlinde.Portal.Backend.Devices

open Gerlinde.Shared.Lib
open Gerlinde.Shared.WebApi
open FsToolkit.ErrorHandling
open Gerlinde.Shared.Repository
open Microsoft.AspNetCore.Http
open Giraffe

module List =
    let handler (organization: Organization.Organization, _) =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                let repo = ctx.GetService<CouchDb.C>()
                let! devices = repo.GetDevicesForOrganization organization.Id
                return! ctx.WriteJsonAsync devices   
            } |> Handler.mapErrorToResponse ctx

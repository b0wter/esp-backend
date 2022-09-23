namespace Gerlinde.Portal.Backend.Organization

open Gerlinde.Shared.Lib
open Gerlinde.Shared.WebApi
open FsToolkit.ErrorHandling
open Gerlinde.Shared.Repository
open Microsoft.AspNetCore.Http
open Giraffe

module Details =
    let handler (organization: Organization.Organization, _) =
        fun (_: HttpFunc) (ctx: HttpContext) ->
            taskResult {
                return! ctx.WriteJsonAsync (organization |> Organization.withoutSensitiveInformation)
            } |> Handler.mapErrorToResponse ctx

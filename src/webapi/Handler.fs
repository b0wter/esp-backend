namespace Gerlinde.Shared.WebApi

open System.Threading.Tasks
open Giraffe
open Microsoft.AspNetCore.Http

module Handler =
    let mapErrorToResponse (ctx: HttpContext) (result: Task<Result<HttpContext option, string>>) : Task<HttpContext option> =
        task {
            match! result with
            | Ok o -> return o
            | Error e ->
                ctx.SetStatusCode 500
                return! ctx.WriteTextAsync e
        }

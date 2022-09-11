namespace Gerlinde.Shared.WebApi

open System
open Giraffe
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http

module Json =
    let tryBindJson<'T> (parsingErrorHandler: string -> HttpHandler) (validator: 'T -> Validation<'T, string>) (successHandler: 'T -> HttpHandler): HttpHandler =
        let inline isNullMatch value = obj.ReferenceEquals(value, null)
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                    try
                        let! model = ctx.BindJsonAsync<'T>()
                        if model |> isNullMatch then
                            return! parsingErrorHandler "The request body is empty" next ctx
                        else
                            match model |> validator with
                            | Validation.Ok t ->
                                return! successHandler t next ctx
                            | Validation.Error errors ->
                                let errorMessage = sprintf $"Validation failed because: %s{String.Join(';', errors)}"
                                return! parsingErrorHandler errorMessage next ctx
                    with ex ->
                        let errorMessage = sprintf $"Malformed request or missing field in request body, reason: %s{ex.Message}"
                        return! parsingErrorHandler errorMessage next ctx
                }

    let tryBindJsonWithExtra<'T, 'U> (parsingErrorHandler: string -> HttpHandler) (validator: 'T -> Validation<'T, string>) (successHandler: 'T -> 'U -> HttpHandler) (extra: 'U): HttpHandler =
        let inline isNullMatch value = obj.ReferenceEquals(value, null)
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                    try
                        let! model = ctx.BindJsonAsync<'T>()
                        if model |> isNullMatch then
                            return! parsingErrorHandler "The request body is empty" next ctx
                        else
                            match model |> validator with
                            | Validation.Ok t ->
                                return! successHandler t extra next ctx
                            | Validation.Error errors ->
                                let errorMessage = sprintf $"Validation failed because: %s{String.Join(';', errors)}"
                                return! parsingErrorHandler errorMessage next ctx
                    with ex ->
                        let errorMessage = sprintf $"Malformed request or missing field in request body, reason: %s{ex.Message}"
                        return! parsingErrorHandler errorMessage next ctx
                }
        



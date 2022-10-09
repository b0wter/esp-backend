namespace Gerlinde.Portal.Cli

open System
open System.Net.Http
open System.Threading.Tasks
open System.Web

module Http =
    let private client = new HttpClient()
    
    module private Task =
        let singleton value = value |> Task.FromResult

        let bind (f : 'a -> Task<'b>) (x : Task<'a>) = task {
            let! x = x
            return! f x
        }

        let map f x = x |> bind (f >> singleton)
            
    let combineUrls (part1: string) (part2: string) =
        let trimmed1 = part1.TrimEnd('/')
        let trimmed2 = part2.TrimStart('/')
        sprintf $"%s{trimmed1}/%s{trimmed2}"
    
    type ApiHttpResponse =
        | Ok of body:string
        | Error of statusCode:int * body:string * headers:string list
        | Exception of e:exn
    
    let sendTextRequest (request: HttpRequestMessage) : Task<ApiHttpResponse> =
        task {
            try 
                let! response = client.SendAsync request
                let! content =
                    try
                        response.Content.ReadAsStringAsync() |> Task.map FSharp.Core.Result.Ok
                    with
                        ex -> Task.FromResult (FSharp.Core.Result.Error ex)
                        
                let headers =
                    response.Headers
                    |> Seq.map (fun x -> $"""%s{x.Key}: %s{String.Join(',', x.Value)}""")
                    |> List.ofSeq
                let statusCode = response.StatusCode |> int
                
                match content with
                | FSharp.Core.Result.Ok body when statusCode >= 200 && statusCode < 400 ->
                    return Ok body
                | FSharp.Core.Result.Ok body ->
                    return Error (statusCode, body, headers)
                | FSharp.Core.Result.Error exn ->
                    return  Exception exn

            with
            | exn ->
                return Exception exn
        }
        
    let createJsonPost (url: string) (payload: obj) : HttpRequestMessage =
        let request = new HttpRequestMessage(HttpMethod.Post, url)
        let content = System.Text.Json.JsonSerializer.Serialize(payload)
        do request.Content <- new StringContent(content)
        request
        
    let createGet (url: string) (queryParameters: (string * string) list) : HttpRequestMessage =
        let url =
            if queryParameters.IsEmpty then url
            else
                let query = HttpUtility.ParseQueryString(String.Empty)
                queryParameters |> List.iter (fun (key, value) -> query[key] <- value)
                query.ToString()
        new HttpRequestMessage(HttpMethod.Get, url)
        
namespace Gerlinde.Portal.Cli

open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Hopac

module Portal =
        
    let login (baseUrl: string) (email: string) (password: string) =
        task {
            let url = Http.combineUrls baseUrl "/login"
            let request = Http.createJsonPost url {| Email = email; Password = password |}
            return! Http.sendTextRequest request
        }

    let logout (baseUrl: string) : Task<Http.ApiHttpResponse> =
        let url = $"%s{baseUrl}/logout"
        let request = Http.createGet url []
        Http.sendTextRequest request
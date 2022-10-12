namespace Gerlinde.Portal.Cli

open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Gerlinde.Shared.Lib
open Hopac

module Portal =
        
    let login (baseUrl: string) (email: string) (password: string) =
        task {
            let url = Http.combineUrls baseUrl "/login"
            let request = Http.createJsonPost url {| Email = email; Password = password |}
            return! Http.sendTextRequest None request
        }

    let logout (baseUrl: string) authToken : Task<Http.ApiHttpResponse> =
        let url = Http.combineUrls baseUrl "/logout"
        let request = Http.createGet url []
        Http.sendTextRequest (Some authToken) request
        
    let addDevice (baseUrl: string) authToken (device: {| MacAddress: string; Name: string option; Description: string option |}) : Task<Http.ApiHttpResponse> =
        let url = Http.combineUrls baseUrl "/devices"
        let request = Http.createJsonPost url device
        Http.sendTextRequest (Some authToken) request
        
    let deleteDevice baseUrl authToken macAddress =
        let url = Http.combineUrls baseUrl $"/devices/%s{macAddress}"
        let request = Http.createDelete url []
        Http.sendTextRequest (Some authToken) request
        
    let deviceDetails baseUrl authToken macAddress =
        let url = Http.combineUrls baseUrl $"/devices/%s{macAddress}"
        let request = Http.createGet url []
        Http.sendTextRequest (Some authToken) request

    let listDevices baseUrl authToken =
        let url = Http.combineUrls baseUrl "/devices"
        let request = Http.createGet url []
        Http.sendTextRequest (Some authToken) request

    let addOrganization baseUrl email name password =
        let url = Http.combineUrls baseUrl "/register"
        let request = Http.createJsonPost url {| Email = email; Name = name; Password = password |}
        Http.sendTextRequest None request
        
    let organizationDetails baseUrl authToken =
        let url = Http.combineUrls baseUrl "/organization"
        let request = Http.createGet url []
        Http.sendTextRequest (Some authToken) request

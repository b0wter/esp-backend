namespace Gerlinde.Shared.Lib

open System
open System.Globalization
open System.Security.Cryptography
open System.Text.RegularExpressions

module Utilities =
    
    let private availableCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray()
    
    let generateToken (length: int) =
        let bytes = RandomNumberGenerator.GetBytes(4 * length)
        let randomNumbers =
            bytes
            |> Array.chunkBySize 4
            |> Array.map (fun bytes -> BitConverter.ToInt32(bytes, 0) |> Math.Abs) // converted to positive Int32s
        let indices =
            randomNumbers
            |> Array.map (fun i -> i % availableCharacters.Length)
        let chars =
            indices
            |> Array.map (fun i -> availableCharacters[i])
        String.Join(String.Empty, chars)

    let isValidEmail (s: string) =
        // Take from:
        // https://docs.microsoft.com/de-de/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format
        if String.IsNullOrWhiteSpace(s) then false
        else
            let mutable email = s
            try
                let DomainMapper (m: Match) =
                    let idn = IdnMapping()
                    let domainName = idn.GetAscii(m.Groups[2].Value)
                    m.Groups[1].Value + domainName

                email <- Regex.Replace(email, @"(@)(.+)$", DomainMapper, RegexOptions.None, TimeSpan.FromMilliseconds(200))
                
                Regex.IsMatch(
                    email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))
            with
            | :? RegexMatchTimeoutException -> false
            | :? ArgumentException -> false
                
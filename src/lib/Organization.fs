namespace Gerlinde.Shared.Lib

open System
open Gerlinde.Shared.Authentication

module Organization =
    
    type AccessToken =
        {
            Token: string
            Name: string option
            ValidThrough: DateOnly
        }
    
    type Organization =
        {
            Id: Guid
            Email: string
            PasswordHash: Hashing.Hash
            Salt: Hashing.Base64Salt
            Name: string
            Devices: Device.Device list
            /// <summary>
            /// Access tokens generated for this organization. Not to be confused with the device access tokens
            /// </summary>
            AccessTokens: AccessToken list
        }
    
    let create email password name =
        let salt = Hashing.salt ()
        let base64Salt = Convert.ToBase64String(match salt with Hashing.Salt s -> s) |> Hashing.Base64Salt
        let hashedPassword = Hashing.hash (Password.Password password) salt
        {
            AccessTokens = []
            Devices = []
            Email = email
            Id = Guid.NewGuid()
            Name = name
            Salt = base64Salt
            PasswordHash = hashedPassword
        }
        
    let createTokenWith name validThrough =
        {
            Token = Utilities.generateToken 64
            Name = name
            ValidThrough = validThrough
        }
    
    let createToken name =
        createTokenWith name (DateOnly.FromDateTime(DateTime.Now.AddMonths(2)))
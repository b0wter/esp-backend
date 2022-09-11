namespace Authentication.Lib

module Hashing =
    open System
    open System.Security.Cryptography
    open Microsoft.AspNetCore.Cryptography.KeyDerivation

    type Password = Password of string
    type Salt = Salt of byte []
    type Base64Salt = Base64Salt of string

    let hello name =
        printfn "Hello %s" name

    let salt () = 
        let bytes : byte[] = Array.zeroCreate (128/8)
        use rng = RandomNumberGenerator.Create()
        rng.GetBytes(bytes)
        bytes |> Salt

    let hash password salt =
        Convert.ToBase64String(KeyDerivation.Pbkdf2(
                                password = password,
                                salt = (match salt with Salt s -> s),
                                prf = KeyDerivationPrf.HMACSHA1,
                                iterationCount = 10000,
                                numBytesRequested = 256/8))
        |> Password
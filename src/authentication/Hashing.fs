namespace Gerlinde.Shared.Authentication

module Hashing =
    open System
    open System.Security.Cryptography
    open Microsoft.AspNetCore.Cryptography.KeyDerivation

    type Hash = Hash of string
    type Salt = Salt of byte []
    type Base64Salt = Base64Salt of string

    let defaultIterationCount = 10000
    let defaultPbkdfBitSize = 256/8
    let defaultSaltBitSize = 128/8

    let saltWith saltSize () = 
        let bytes : byte[] = Array.zeroCreate saltSize
        use rng = RandomNumberGenerator.Create()
        rng.GetBytes(bytes)
        bytes |> Salt

    let salt = saltWith defaultSaltBitSize

    let hashWith iterations bitSize (password: Password.Password) salt : Hash =
        Convert.ToBase64String(KeyDerivation.Pbkdf2(
                                password = (password |> Password.value), 
                                salt = (match salt with Salt s -> s),
                                prf = KeyDerivationPrf.HMACSHA1,
                                iterationCount = iterations,
                                numBytesRequested = bitSize))
        |> Hash

    let hash = hashWith defaultIterationCount defaultPbkdfBitSize
    
    let hashAsString = function Hash h -> h
    
    let base64SaltAsString = function Base64Salt s -> s
namespace Gerlinde.Shared.Authentication

module Password =
    open System

    type Password = Password of string
    type PasswordValidation = (string -> bool) * string

    let defaultPasswordLengthRequirement = 8

    let defaultCriteriaWith passwordLength : PasswordValidation list = [
        (fun s -> s.Length > passwordLength),       (sprintf "Das Passwort muss mindestens %i Zeichen lang sein." passwordLength)
        (fun s -> s |> Seq.exists Char.IsUpper),    "Dass Passwort muss mindestens einen Großbuchstaben enthalten."
        (fun s -> s |> Seq.exists Char.IsLower),    "Das Passwort muss mindestens einen Kleinbuchstaben enthalten."
        (fun s -> s |> Seq.exists Char.IsNumber),   "Das Passwort muss mindestens eine Zahl enthalten."
        (fun s -> s |> Seq.exists Char.IsLetter),   "Das Passwort muss mindestens einen Buchstaben enthalten."
    ]

    let defaultCriteria = defaultCriteriaWith defaultPasswordLengthRequirement

    let rec validate (validations: PasswordValidation list) s =
        match validations with
        | [] -> Ok s
        | (predicate, description) :: tail -> if predicate s then Error description
                                              else validate tail s

    let createWith (validations: PasswordValidation list) (s: string) =
        validate validations s

    let create = createWith defaultCriteria

    let value (Password p) = p
        
    let private randomString n =
        let r = Random()
        let smallLetters = [|'a' .. 'z'|]
        let capitalLetters = [|'A' .. 'Z'|]
        let numbers = [|'0' .. '9'|]
        let chars = Array.concat([smallLetters; capitalLetters; numbers])
        let sz = Array.length chars in
        String(Array.init n (fun _ -> chars.[r.Next sz]))

    let random () =
        Password (randomString 16)
        
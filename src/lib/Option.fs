namespace Gerlinde.Shared.Lib

module Option =
    
    let fromBool b =
        if b then Some ()
        else None


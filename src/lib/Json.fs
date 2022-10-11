namespace Gerlinde.Shared.Lib

open System
open System.Globalization
open Newtonsoft.Json

module Json =
    
    type DateOnlyJsonConverter() =
        inherit JsonConverter<DateOnly>()
        let format = "yyyy-MM-dd"

        override this.WriteJson(writer: JsonWriter, value: DateOnly, _: JsonSerializer): unit =
            writer.WriteValue(value.ToString(format, CultureInfo.InvariantCulture))

        override this.ReadJson(reader, _, _, _, _) =
            DateOnly.ParseExact(reader.Value :?> string, format, CultureInfo.InvariantCulture)
        
    type TimeOnlyJsonConverter() =
        inherit JsonConverter<TimeOnly>()
        let format = "HH:mm:ss.FFFFFFF"

        override this.WriteJson(writer: JsonWriter, value: TimeOnly, _: JsonSerializer): unit =
            writer.WriteValue(value.ToString(format, CultureInfo.InvariantCulture))
        override this.ReadJson(reader, _, _, _, _) =
            TimeOnly.ParseExact(reader.Value :?> string, format, CultureInfo.InvariantCulture)


This is an experimental backend service to offer an easy way to remote provide other devices with firmware downloads.

## Configuration
This app currently requires a CouchDb instance to persist all of the device/organization information. The connection to the database needs to be set in an `appsettings.json` file:
```
{
    "CouchDb": {
        "Username": "admin",
        "Password": "your password here",
        "Host": "localhost",
        "Port": "5984",
        "Devices": "devices",
        "Organizations": "organizations"
    },
	...
}
```

[Swagger](https://app.swaggerhub.com/apis/b0wter/esp_backend/1.0.0#/)

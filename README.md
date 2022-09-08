This is an experimental backend service to offer an easy way to remote provide other devices with firmware downloads.

## Configuration
This app currently requires a CouchDb instance to persist all of the device/organization information. The connection to the database needs to be set in either an `appsettings.json` file:
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
or as envirnment variables:
```
COUCHDB__USERNAME
COUCHDB__PASSWORD
COUCHDB__HOST
COUCHDB__PORT
...
```

[Swagger](https://app.swaggerhub.com/apis/b0wter/esp_backend/1.0.0#/)

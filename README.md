PI WriteBack
===================

_**This is a repository contains sample code, not advised for production use cases.**_

This sample code shows how to read data points from CDF time series and insert them into OSIsoft PI tags.

## Requirements
* Visual Studio or necessary tools to build .**NET Framework 4.7** applications
* OSIsoft **AF SDK** installed.

## Configuration
A configuration file is expected to be found at `./config/config.yml` path. The contents are as follows:

```yaml
version: 1

logger:
  console:
    level: "debug"

# CDF configuration
cognite:
  project: ${COGNITE_PROJECT}
  host: ${COGNITE_HOST}
  idp-authentication:
    tenant: ${AD_TENANT}
    client-id: ${AD_CLIENT_ID}
    secret: ${AD_CLIENT_SECRET}
    scopes:
      - ${AD_SCOPE}

# PI Server configuration
pi:
  host: ${PI_HOST}
  username: ${PI_USER}
  password: ${PI_PASS}

# External IDs of the time series to be ingested into the PI Server
time-series:
  external-ids:
    - "TimeSeries-External-ID-1"
    - "TimeSeries-External-ID-2"
    - "TimeSeries-External-ID-3"

state-store:
  location: "state.db"
  database: LiteDb
```

In the configuration above, the values within ```${}``` are read from environment variables. This yml file is parsed at runtime to the object defined in [Config.cs](PiWriteBack/Config.cs)

# Running the application
The entry point is [Program.cs](PiWriteBack/Program.cs). No argument is required for the ```Main()``` method. This can be executed from the command line. The application will run continuously until ```Ctrl+C``` is pressed.

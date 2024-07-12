# WeatherForecast

## Create and apply a new Database Migration

## Update the generated ApiClient

### Prerequisites
- `dotnet new tool-manifest`
- `dotnet tool install NSwag.ConsoleCore`

The tools manifest can be found [here](../../../.config/dotnet-tools.json)

### Install the dotnet tools
- `dotnet tool restore`

### Start the web project
- `dotnet run --project '.\examples\WeatherForecast\WeatherForecast.Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj'`

### Update the swagger file
- `dotnet nswag run '.\examples\WeatherForecast\WeatherForecast.Presentation.Web.Server\nswag.json'`

Rebuild the solution and the ApiClient should be updated. For details see the OpenApiReference target in the Client project.
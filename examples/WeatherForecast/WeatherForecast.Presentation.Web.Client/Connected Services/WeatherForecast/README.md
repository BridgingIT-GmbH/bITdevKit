### Update the generated API client:
- update API client:
- Install [NSwagStudio](https://github.com/RicoSuter/NSwag/releases)
- Start the `WeatherForecast.Presentation.Web.Server` project, this will host the backend API and it's swagger documentation
- Open a `cmd` console (in .\examples\WeatherForecast.Presentation.Web.Client\Connected Services\WeatherForecast)
- `nswag run /runtime:Net60`
- [ApiClient.cs](.\examples\WeatherForecast.Presentation.Web.Client\Connected Services\WeatherForecast\ApiClient.cs) has been updated according to the swagger documentation.
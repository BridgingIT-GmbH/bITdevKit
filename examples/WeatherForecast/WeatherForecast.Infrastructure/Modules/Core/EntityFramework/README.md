These database commands should be executed from the solution root folder.

### new migration: 
- `dotnet ef migrations add Initial --context CoreDbContext --output-dir .\Modules\Core\EntityFramework\Migrations --project .\examples\WeatherForecast.Infrastructure\WeatherForecast.Infrastructure.csproj --startup-project .\examples\WeatherForecast.Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj`

### update database: 
- `dotnet ef database update --project .\examples\WeatherForecast.Infrastructure\WeatherForecast.Infrastructure.csproj --startup-project .\examples\WeatherForecast.Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj`
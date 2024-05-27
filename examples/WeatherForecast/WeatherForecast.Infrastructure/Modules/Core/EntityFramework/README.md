These database commands should be executed from the solution root folder.

### new migration:
- `dotnet ef migrations add Initial --context CoreDbContext --output-dir .\Modules\Core\EntityFramework\Migrations --project .\examples\WeatherForecast.Infrastructure\WeatherForecast.Infrastructure.csproj --startup-project .\examples\WeatherForecast.Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj`

### update database:
- `dotnet ef database update --project .\examples\WeatherForecast.Infrastructure\WeatherForecast.Infrastructure.csproj --startup-project .\examples\WeatherForecast.Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj`

### generate migrations script:
- 'dotnet ef migrations script --context CoreDbContext --output .\examples\WeatherForecast\Modules\Core\Core.Infrastructure\EntityFramework\Migrations\efscript_core.sql --project .\examples\WeatherForecast\Modules\Core\Core.Infrastructure\WeatherForecast.Core.Infrastructure.csproj --startup-project .\examples\WeatherForecast\Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj --idempotent'

### generat emigrations bundle:
- 'dotnet ef migrations bundle --context CoreDbContext --output .\examples\WeatherForecast\Modules\Core\Core.Infrastructure\EntityFramework\Migrations\efbundle_core.exe --project .\examples\WeatherForecast\Modules\Core\Core.Infrastructure\WeatherForecast.Core.Infrastructure.csproj --startup-project .\examples\WeatherForecast\Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj --configuration DEBUG'
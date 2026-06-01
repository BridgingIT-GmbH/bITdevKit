These database commands should be executed from the solution root folder.

### new migration:

```bash
dotnet ef migrations add Initial --context CoreDbContext --output-dir .\Modules\Core\EntityFramework\Migrations --project .\examples\WeatherFiesta\WeatherFiesta.Infrastructure\WeatherFiesta.Infrastructure.csproj --startup-project .\examples\WeatherFiesta\WeatherFiesta.Presentation.Web.Server\WeatherFiesta.Presentation.Web.Server.csproj --no-build --verbose
```

### update database:

```bash
dotnet ef database update --project .\examples\WeatherFiesta\WeatherFiesta.Infrastructure\WeatherFiesta.Infrastructure.csproj --startup-project .\examples\WeatherFiesta\WeatherFiesta.Presentation.Web.Server\WeatherFiesta.Presentation.Web.Server.csproj --no-build --verbose
```

### generate migrations script:

```bash
dotnet ef migrations script --context CoreDbContext --output .\examples\WeatherFiesta\WeatherFiesta.Infrastructure\Modules\Core\EntityFramework\Migrations\efscript_core.sql --project .\examples\WeatherFiesta\WeatherFiesta.Infrastructure\WeatherFiesta.Infrastructure.csproj --startup-project .\examples\WeatherFiesta\WeatherFiesta.Presentation.Web.Server\WeatherFiesta.Presentation.Web.Server.csproj --idempotent --no-build --verbose
```

### generate emigrations bundle:

```bash
dotnet ef migrations bundle --context CoreDbContext --output .\examples\WeatherFiesta\WeatherFiesta.Infrastructure\Modules\Core\EntityFramework\Migrations\efbundle_core.exe --project .\examples\WeatherFiesta\WeatherFiesta.Infrastructure\WeatherFiesta.Infrastructure.csproj --startup-project .\examples\WeatherFiesta\WeatherFiesta.Presentation.Web.Server\WeatherFiesta.Presentation.Web.Server.csproj --configuration DEBUG --no-build --verbose
```

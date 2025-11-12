These database commands should be executed from the solution root folder.

### new migration:

```bash
dotnet ef migrations add Initial --context CoreDbContext --output-dir .\Modules\Core\EntityFramework\Migrations --project .\examples\DoFiesta\DoFiesta.Infrastructure\DoFiesta.Infrastructure.csproj --startup-project .\examples\DoFiesta\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj --no-build --verbose
```

### update database:

```bash
dotnet ef database update --project .\examples\DoFiesta\DoFiesta.Infrastructure\DoFiesta.Infrastructure.csproj --startup-project .\examples\DoFiesta\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj --no-build --verbose
```

### generate migrations script:

```bash
dotnet ef migrations script --context CoreDbContext --output .\examples\DoFiesta\DoFiesta.Infrastructure\Modules\Core\EntityFramework\Migrations\efscript_core.sql --project .\examples\DoFiesta\DoFiesta.Infrastructure\DoFiesta.Infrastructure.csproj --startup-project .\examples\DoFiesta\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj --idempotent --no-build --verbose
```

### generate emigrations bundle:

```bash
dotnet ef migrations bundle --context CoreDbContext --output .\examples\DoFiesta\DoFiesta.Infrastructure\Modules\Core\EntityFramework\Migrations\efbundle_core.exe --project .\examples\DoFiesta\DoFiesta.Infrastructure\DoFiesta.Infrastructure.csproj --startup-project .\examples\DoFiesta\DoFiesta.Presentation.Web.Server\DoFiesta.Presentation.Web.Server.csproj --configuration DEBUG --no-build --verbose
```

These database commands should be executed from the solution root folder.

### new migration:
- `dotnet ef migrations add Initial --context MarketingDbContext --output-dir .\EntityFramework\Migrations --project .\examples\DinnerFiesta\Modules\Marketing\Marketing.Infrastructure\DinnerFiesta.Marketing.Infrastructure.csproj --startup-project .\examples\DinnerFiesta\Presentation.Web.Server\DinnerFiesta.Presentation.Web.Server.csproj`

### update database:
- `dotnet ef database update --project .\examples\DinnerFiesta\Modules\Marketing\Marketing.Infrastructure\DinnerFiesta.Marketing.Infrastructure.csproj --startup-project .\examples\DinnerFiesta\Presentation.Web.Server\DinnerFiesta.Presentation.Web.Server.csproj`

### generate migrations script:
- 'dotnet ef migrations script --context MarketingDbContext --output .\examples\DinnerFiesta\Modules\Marketing\Marketing.Infrastructure\EntityFramework\Migrations\efscript_core.sql --project .\examples\DinnerFiesta\Modules\Marketing\Marketing.Infrastructure\DinnerFiesta.Marketing.Infrastructure.csproj --startup-project .\examples\DinnerFiesta\Presentation.Web.Server\DinnerFiesta.Presentation.Web.Server.csproj --idempotent'

### generat emigrations bundle:
- 'dotnet ef migrations bundle --context MarketingDbContext --output .\examples\DinnerFiesta\Modules\Marketing\Marketing.Infrastructure\EntityFramework\Migrations\efbundle_core.exe --project .\examples\DinnerFiesta\Modules\Marketing\Marketing.Infrastructure\DinnerFiesta.Marketing.Infrastructure.csproj --startup-project .\examples\DinnerFiesta\Presentation.Web.Server\DinnerFiesta.Presentation.Web.Server.csproj --configuration DEBUG'
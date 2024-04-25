These database commands should be executed from the solution root folder.

### new migration: 
- `dotnet ef migrations add Initial --context CoreDbContext --output-dir .\EntityFramework\Migrations --project .\examples\DinnerFiesta\Modules\Core\Core.Infrastructure\DinnerFiesta.Core.Infrastructure.csproj --startup-project .\examples\DinnerFiesta\Presentation.Web.Server\DinnerFiesta.Presentation.Web.Server.csproj`

### update database: 
- `dotnet ef database update --project .\examples\DinnerFiesta\Modules\Core\Core.Infrastructure\DinnerFiesta.Core.Infrastructure.csproj --startup-project .\examples\DinnerFiesta\Presentation.Web.Server\DinnerFiesta.Presentation.Web.Server.csproj`
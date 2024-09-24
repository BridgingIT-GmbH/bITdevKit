These database commands should be executed from the solution root folder.

### new migration:

-
`dotnet ef migrations add Initial --context StubDbContext --output-dir .\Migrations --project .\tests\Application.IntegrationTests\Application.IntegrationTests.csproj`
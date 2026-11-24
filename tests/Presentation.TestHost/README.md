# Presentation test host

Run the server host from the repository root. The AppState probes at `/` and `/lifecycle` are available by default.

```sh
dotnet run --project tests/Presentation.TestHost/Presentation.TestHost.csproj -- --urls=http://localhost:5080 --TestScenarios:ProblemDetails:Profile=Api
```

Use `Profile=Mixed` to exercise the API alongside native HTML error pages. Add `--TestScenarios:ProblemDetails:IncludeExceptionDetails=true` to enable diagnostics for the fixture. The flag defaults to false.

| Path or method | Behavior |
| --- | --- |
| GET `/api/problem-details/throw/validation` and `/api/problem-details/result/validation` | Grouped validation messages |
| GET `/api/problem-details/throw/server` and `/api/problem-details/result/server` | Safe detail and optional diagnostics |
| GET `/api/problem-details/throw/mapped` | Application-mapped 409 |
| GET `/api/problem-details/empty` and `/api/problem-details/explicit` | An automatic 404 problem and an explicit 404 body |
| POST `/problem-details/ui/throw` and GET `/problem-details/ui/unmatched` | Native HTML error and status pages in the mixed profile |
| GET `/openapi/problem-details.json` | Response schemas for the enabled fixture |

To run the automated HTTP and OpenAPI checks, use `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --filter FullyQualifiedName~ProblemDetails`.

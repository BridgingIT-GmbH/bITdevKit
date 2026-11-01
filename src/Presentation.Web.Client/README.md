![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you're working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

Components are available as [NuGet packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [CHANGELOG](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/CHANGELOG.md).

Join us in advancing the world of software development with the bITDevKit!

## NDJSON over HttpClient

The reusable .NET NDJSON helpers live in `Common.Utilities`: `GetNdjsonAsync<T>` and `OpenNdjsonAsync<T>` open one owned response using `ResponseHeadersRead`. Consume it with `ReadAsync`, `ReadRecordsAsync`, `ReadEnvelopesAsync`, or `ReadDataAsync`, and `await using` the response. Keep the existing authenticated client; generated Kiota JSON clients do not parse NDJSON. For a fictional inventory dashboard, consume `/api/inventory/stream/enveloped` with the existing authenticated client and keep a bounded history of stock records. See the [Presentation Streaming guide](../../docs/features-presentation-streaming.md).

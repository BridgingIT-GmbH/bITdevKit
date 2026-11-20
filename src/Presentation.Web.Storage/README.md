![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you're working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

This repository includes the complete source code for the bITDevKit, along with a variety of sample
applications located in the ./examples folder within the solution. These samples serve as practical
demonstrations of how to leverage the capabilities of the bITDevKit in real-world scenarios. All
components are available
as [nuget packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [CHANGELOG](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/CHANGELOG.md).

Join us in advancing the world of software development with the bITDevKit!

## Storage Dimensions

Existing opt-in storage endpoint groups expose typed Dimensions alongside Properties. Read groups add POST searches; document reads also expose filtered keys and counts. Maintenance groups add conditional dimension replacement through PATCH. Blob content remains streamed, with optional typed `X-Storage-Dimensions` metadata limited by the host's header budget.

Wire values use explicit type/value envelopes, including invariant strings for exact decimals. Filters accept bounded `all`/`any` groups and typed operands. Invalid input returns 400, conflicting ETags return 409, and oversized blob metadata headers return 431. Existing endpoint authorization and enablement apply. Dashboard explorers provide typed Dimension query controls; Dimension mutation editing remains outside scope.

Dashboard filters travel in URLs. Large filters can exceed the host's request-line or query-string limit and return HTTP 414 even when their logical values are valid. This transport limit is accepted for the dashboards; use the POST search endpoints or .NET query APIs for larger filters.

See the [blob](../../docs/features-storage-blobs.md#queryable-dimensions) and [document](../../docs/features-storage-documents.md#queryable-dimensions) guides for wire examples and effective provider limits. EF relational providers currently support the feature; inspect capabilities before accepting Dimensions on another backend.

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
## Storage properties

Blob and document `Properties` are optional scalar metadata. Null preserves the existing bag on replacement/update, an empty bag clears it, and a non-empty bag replaces it. Inserts without properties return an empty bag. Typed .NET calls preserve the `PropertyBagScalarCodec` scalar types and copy byte arrays. HTTP retains ordinary JSON semantics. Transform metadata is separate from application properties. Properties are not indexed; Dimensions remain a separate planned feature.

The blob provider contracts and persisted blob formats have changed. Use newly created storage; this refactor supplies no migrations, backfill, or compatibility readers. See the [blob guide](../../docs/features-storage-blobs.md#properties) and [document guide](../../docs/features-storage-documents.md).

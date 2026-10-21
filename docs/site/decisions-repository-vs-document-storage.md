---
title: Repository or Document Storage
---

<img class="bdk-doc-icon" src="assets/features/repositories.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/storage-documents.svg" width="32" height="32" alt="" align="right">

# Repository or Document Storage

Choose based on the application model and access pattern. Repositories persist entities through entity-oriented contracts. Document Storage reads and writes typed documents through a storage client.

## Choose Repository when

- the application loads entities or aggregates and invokes their behavior
- specifications, projections, ordering, paging, or related-entity includes shape queries
- repository behaviors should publish domain events or capture changes
- entity persistence belongs to the module's domain model

`IGenericRepository<TEntity>` provides entity persistence. `IGenericReadOnlyRepository<TEntity>` covers reads. Aggregate methods still own invariant checks; the repository does not enforce business rules in place of the model.

## Choose Document Storage when

- data is a typed configuration document, archive, snapshot, or stored transfer object
- a partition and row key identify the document
- serialization, ETags, expiration, hashes, and bounded paging fit the use case
- the application does not need a repository's entity-query contract for this data

`IDocumentStoreClient<T>` serializes typed values and uses `DocumentKey` for identity. The client supports provider-neutral operations rather than general queries over arbitrary document-body fields.

## Comparison

| Concern | Repository | Document Storage |
| --- | --- | --- |
| Stored model | Entity or aggregate | Typed document |
| Identity | Entity identifier | Partition and row keys |
| Queries | Specifications and `FindOptions<TEntity>`, subject to provider support | Key-oriented queries and bounded continuation-token paging |
| Persistence behavior | Configured repository decorators | Configured client behaviors and provider operations |
| Typical use | Order aggregate with business methods and relationships | Weather archive or shared configuration document |

## Example

Load an order through a repository, call its confirmation method, and persist the result. Repository behaviors can then publish registered domain events.

Store a typed weather archive through Document Storage when consumers read or replace the archive by key. Its format and retention policy belong to the storage contract.

## Provider choice is a separate decision

Both features can use database or cloud providers. Using Cosmos or Entity Framework does not by itself decide which abstraction fits.

A module can use both: a repository for its aggregates and Document Storage for archive documents. Choose the contract before choosing its persistence provider.

## Related features

- [Domain Repositories](reference/features-domain-repositories.md)
- [Document Storage](reference/features-storage-documents.md)
- [Repository or ActiveEntity](decisions-repository-vs-activeentity.md)
- [Blob Storage or Document Storage](decisions-blob-vs-document-storage.md)

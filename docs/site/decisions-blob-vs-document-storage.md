---
title: Blob Storage or Document Storage
---

<img class="bdk-doc-icon" src="assets/features/storage-blobs.svg" width="32" height="32" alt="" align="right"> <img class="bdk-doc-icon" src="assets/features/storage-documents.svg" width="32" height="32" alt="" align="right">

# Blob Storage or Document Storage

Choose based on how application code reads and writes the content. Blob Storage exposes streams. Document Storage serializes and reads typed documents.

## Choose Blob Storage when

- the payload is an image, PDF, attachment, export, or other binary content
- callers upload or download a stream
- the application treats the content as an opaque artifact
- content type, metadata, and transfer limits matter more than a typed document contract

Consumers resolve a named `IBlobStoreClient` through `IBlobStoreClientFactory`. A `BlobKey` identifies the content.

## Choose Document Storage when

- the application writes and reads a known document type
- serialization and deserialization belong to the storage client
- configuration, reference data, snapshots, or temporary typed records need storage
- a `DocumentKey` with partition and row keys fits the access pattern

Consumers use `IDocumentStoreClient<T>`. The client owns the document contract even when its provider stores serialized bytes in a blob backend.

## Comparison

| Concern | Blob Storage | Document Storage |
| --- | --- | --- |
| Application API | Upload and download streams | Read and write typed values |
| Identity | `BlobKey` | `DocumentKey` with partition and row keys |
| Serialization | Application owns the payload format | Client serializes and deserializes the document |
| Shared capabilities | Metadata, ETags, hashes, expiration, and bounded listing | Metadata, ETags, hashes, expiration, and bounded paging |
| Typical use | Generated report or uploaded image | Configuration document or typed archive record |

## Example

Store a generated weather-report PDF in Blob Storage. Store a weather archive that the application reads as a typed document in Document Storage.

A JSON file can fit either model. Use Blob Storage when callers transfer the file unchanged. Use Document Storage when callers work with its typed contents.

For aggregate persistence with business invariants and repository queries, see [Repository or ActiveEntity](decisions-repository-vs-activeentity.md).

## Related features

- [Blob Storage](reference/features-storage-blobs.md)
- [Document Storage](reference/features-storage-documents.md)

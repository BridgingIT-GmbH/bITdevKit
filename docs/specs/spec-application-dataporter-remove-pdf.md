---
created: 2026-10-19
status: draft
---

# Remove PDFsharp and built-in DataPorter PDF export

## Goal

As a DataPorter consumer, I want to use the package without PDFsharp dependencies or PDF-specific licensing requirements.

Remove built-in PDF export while retaining Excel, CSV, typed CSV, JSON, XML, and custom providers. PDFsharp 6.5 introduces [AGPL or commercial license activation](https://docs.pdfsharp.net/General/History/README-v6.5.html); this change also removes the existing rendering and font-resolution maintenance burden.

This specification is for later implementation. No replacement PDF library or optional PDF package is required.

## Required changes

1. Remove `src/Application.DataPorter/Providers/Pdf/`, including the provider, configuration, PDF enums, font resolver, and `WithPdf()` registration overloads. Remove `Format.Pdf`, its entry in `Format.BuiltIns`, and `AsPdf()` from both `ExportOptionsBuilder` and `TemplateOptionsBuilder`.
2. Remove the `PDFsharp-MigraDoc` reference from `Application.DataPorter.csproj` and its central version and holdback comment from `Directory.Packages.props`. Restore the solution to update lock files. Keep the `System.Security.Cryptography.Pkcs` security pin because SQLClient and MimeKit still require it; update its comment to reflect the remaining dependencies.
3. Remove DoFiesta's `.WithPdf()` configuration in `CoreModule.cs`, `DataPorter:Pdf` settings, and PDF choice in `TodoItemIndex.razor`. Remove PDF-specific MIME mapping and supported-format text from `CoreDataPorterEndpoints.cs`. Preserve its generic export endpoint and query handler.
4. Remove PDF-specific tests and PDF cases from shared DataPorter theories. Preserve coverage for the remaining formats and custom providers. Remove obsolete PDF mentions from shared XML documentation without removing options still used by Excel, such as column widths.
5. Update the [DataPorter guide](../features-application-dataporter.md). Reconcile the [templating specification](spec-application-templating.md) by removing the proposed PDFsharp/MigraDoc dependency, package, and renderer examples. Preserve its renderer-neutral design and ability to support future custom PDF renderers.
6. Document the removed public APIs and PDF export capability as a breaking change in the release notes. Existing consumers must select another export format or provide their own implementation.

## Acceptance criteria

- When the solution is restored, no PDFsharp or MigraDoc package remains in the dependency graph.
- When DoFiesta's export dialog opens, PDF is absent and its remaining formats still export successfully.
- Given no custom PDF provider, exporting with `new Format("pdf")` returns the existing `FormatNotSupportedError`; DoFiesta's `/api/core/todoitems/dataporter/export/pdf` returns HTTP 400 without advertising PDF support. Keep `Format.TryParse` extensible rather than adding a PDF-specific rejection.
- Existing import/export behavior for retained formats and custom providers passes its tests. PDFsharp types and removed convenience APIs are absent from the public API.
- The solution builds, relevant unit and endpoint tests pass, and the dependency vulnerability scan remains clean. Documentation no longer promises built-in PDF export or a PDFsharp-based templating implementation.

## Boundaries

Do not change generic PDF file storage, downloads, MIME recognition, or existing user documents. No database migration, new project, replacement renderer, or compatibility shim is part of this removal.

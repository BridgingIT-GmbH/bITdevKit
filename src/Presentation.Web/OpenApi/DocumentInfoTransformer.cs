// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using System.Reflection;

/// <summary>
/// Customizes the OpenAPI document information including title, version, and description.
/// </summary>
/// <remarks>
/// <para>
/// This transformer configures the OpenAPI document's info section with customized
/// title, version, description, and other metadata. It can be configured through
/// dependency injection or use sensible defaults.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Sets the API title</item>
/// <item>Sets the API version</item>
/// <item>Adds or updates the API description</item>
/// <item>Optionally adds contact and license information</item>
/// <item>Can extract version from assembly or configuration</item>
/// </list>
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="DocumentInfoTransformer"/> class.
/// </remarks>
/// <param name="options">Configuration options for the document info. If null, uses defaults.</param>
public class DocumentInfoTransformer(DocumentInfoOptions options = null) : IOpenApiDocumentTransformer
{
    private readonly DocumentInfoOptions options = options ?? new DocumentInfoOptions();

    /// <summary>
    /// Transforms the OpenAPI document to customize its information section.
    /// </summary>
    /// <param name="document">The OpenAPI document being transformed.</param>
    /// <param name="context">Context information about the document transformation.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task representing the transformation operation.</returns>
    /// <remarks>
    /// Updates the document's info section with customized title, version, description,
    /// contact, and license information according to the configured options.
    /// </remarks>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Set title
        if (!string.IsNullOrWhiteSpace(this.options.Title))
        {
            document.Info.Title = this.options.Title;
        }

        // Set version
        if (!string.IsNullOrWhiteSpace(this.options.Version))
        {
            document.Info.Version = this.options.Version;
        }
        if (this.options.UseAssemblyVersion)
        {
            document.Info.Version = this.GetAssemblyVersion();
        }

        // Set description
        if (!string.IsNullOrWhiteSpace(this.options.Description))
        {
            document.Info.Description = this.options.Description;
        }

        // Set contact
        if (this.options.Contact != null)
        {
            document.Info.Contact = new OpenApiContact
            {
                Name = this.options.Contact.Name,
                Email = this.options.Contact.Email,
                Url = this.options.Contact.Url
            };
        }

        // Set license
        if (this.options.License != null)
        {
            document.Info.License = new OpenApiLicense
            {
                Name = this.options.License.Name,
                Url = this.options.License.Url
            };
        }

        // Set terms of service
        if (this.options.TermsOfServiceUrl != null)
        {
            document.Info.TermsOfService = this.options.TermsOfServiceUrl;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Extracts the version from the assembly.
    /// </summary>
    /// <returns>The assembly version string.</returns>
    private string GetAssemblyVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version?.ToString() ?? "1.0.0.0";

        // Remove the build and revision if they are .0
        var parts = version.Split('.');
        if (parts.Length >= 2 && parts[2] == "0" && parts[3] == "0")
        {
            return $"{parts[0]}.{parts[1]}";
        }

        return version;
    }
}

/// <summary>
/// Configuration options for the <see cref="DocumentInfoTransformer"/>.
/// </summary>
public class DocumentInfoOptions
{
    /// <summary>
    /// Gets or sets the API title.
    /// </summary>
    /// <value>
    /// The title to display in the OpenAPI documentation.
    /// Defaults to null (keeps the original title).
    /// </value>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the API version.
    /// </summary>
    /// <value>
    /// The version string (e.g., "1.0.0", "2.5.1").
    /// Defaults to null.
    /// </value>
    public string Version { get; set; } = "v1.0";

    /// <summary>
    /// Gets or sets a value indicating whether to use the assembly version.
    /// </summary>
    /// <value>
    /// True to automatically extract the version from the entry assembly.
    /// Defaults to false. Ignored if Version is explicitly set.
    /// </value>
    public bool UseAssemblyVersion { get; set; }

    /// <summary>
    /// Gets or sets the API description.
    /// </summary>
    /// <value>
    /// A detailed description of the API displayed in the documentation.
    /// Supports markdown formatting. Defaults to null.
    /// </value>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the contact information for the API.
    /// </summary>
    /// <value>
    /// Contact details including name, email, and URL. Defaults to null.
    /// </value>
    public ContactInfo Contact { get; set; }

    /// <summary>
    /// Gets or sets the license information for the API.
    /// </summary>
    /// <value>
    /// License details including name and URL. Defaults to null.
    /// </value>
    public LicenseInfo License { get; set; }

    /// <summary>
    /// Gets or sets the terms of service URL.
    /// </summary>
    /// <value>
    /// A URL pointing to the terms of service document. Defaults to null.
    /// </value>
    public Uri TermsOfServiceUrl { get; set; }
}

/// <summary>
/// Contact information for the API.
/// </summary>
public class ContactInfo
{
    /// <summary>
    /// Gets or sets the contact name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the contact email.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the contact URL.
    /// </summary>
    public Uri Url { get; set; }
}

/// <summary>
/// License information for the API.
/// </summary>
public class LicenseInfo
{
    /// <summary>
    /// Gets or sets the license name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the license URL.
    /// </summary>
    public Uri Url { get; set; }
}
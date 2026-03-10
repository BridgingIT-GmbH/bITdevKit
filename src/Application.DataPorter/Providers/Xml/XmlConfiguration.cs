// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Xml;

/// <summary>
/// Configuration options for the XML provider.
/// </summary>
public sealed class XmlConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to format the XML with indentation.
    /// </summary>
    public bool WriteIndented { get; set; } = true;

    /// <summary>
    /// Gets or sets the root element name for collections.
    /// </summary>
    public string RootElementName { get; set; } = "Items";

    /// <summary>
    /// Gets or sets the item element name for each record.
    /// </summary>
    public string ItemElementName { get; set; } = "Item";

    /// <summary>
    /// Gets or sets a value indicating whether to omit the XML declaration.
    /// </summary>
    public bool OmitXmlDeclaration { get; set; } = false;

    /// <summary>
    /// Gets or sets the encoding for the XML document.
    /// </summary>
    public string Encoding { get; set; } = "utf-8";

    /// <summary>
    /// Gets or sets a value indicating whether to use attributes instead of elements for simple properties.
    /// </summary>
    public bool UseAttributes { get; set; } = false;

    /// <summary>
    /// Gets or sets the date format string.
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>
    /// Gets the XmlWriterSettings based on this configuration.
    /// </summary>
    public XmlWriterSettings GetWriterSettings()
    {
        return new XmlWriterSettings
        {
            Indent = this.WriteIndented,
            OmitXmlDeclaration = this.OmitXmlDeclaration,
            Encoding = System.Text.Encoding.GetEncoding(this.Encoding)
        };
    }

    /// <summary>
    /// Gets the XmlReaderSettings based on this configuration.
    /// </summary>
    public XmlReaderSettings GetReaderSettings()
    {
        return new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true
        };
    }
}

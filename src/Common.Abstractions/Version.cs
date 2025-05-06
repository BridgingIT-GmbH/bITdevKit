// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// Represents a Semantic Version (SemVer) with major, minor, patch, prerelease, and build metadata components.
/// Provides methods for parsing, comparing, and rendering version strings.
/// </summary>
public class Version : IComparable<Version>, IEquatable<Version>
{
    /// <summary>
    /// Gets or sets the major version number.
    /// </summary>
    public int Major { get; set; }

    /// <summary>
    /// Gets or sets the minor version number.
    /// </summary>
    public int Minor { get; set; }

    /// <summary>
    /// Gets or sets the patch version number.
    /// </summary>
    public int Patch { get; set; }

    /// <summary>
    /// Gets or sets the prerelease identifier (e.g., "alpha", "beta.1").
    /// </summary>
    public string Prerelease { get; set; }

    /// <summary>
    /// Gets or sets the build metadata (e.g., "build123").
    /// </summary>
    public string BuildMetadata { get; set; }

    private static readonly Version DefaultVersion = new() { Major = 0, Minor = 0, Patch = 0 };

    /// <summary>
    /// Initializes a new instance of the <see cref="Version"/> class with default values (0.0.0).
    /// </summary>
    public Version()
    {
        this.Major = 0;
        this.Minor = 0;
        this.Patch = 0;
        this.Prerelease = null;
        this.BuildMetadata = null;
    }

    /// <summary>
    /// Gets a value indicating whether the version is valid (not the default 0.0.0).
    /// </summary>
    public bool IsValid => this.Major != 0 || this.Minor != 0 || this.Patch != 0;

    /// <summary>
    /// Parses a Semantic Version string into a <see cref="Version"/> object.
    /// Returns the default version (0.0.0) if the input is invalid.
    /// </summary>
    /// <param name="version">The version string to parse (e.g., "1.0.0-alpha+build123").</param>
    /// <returns>A <see cref="Version"/> object representing the parsed version, or 0.0.0 if invalid.</returns>
    public static Version Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return DefaultVersion;
        }

        var regex = new Regex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(-(?<prerelease>[0-9A-Za-z-\.]+))?(\+(?<buildmetadata>[0-9A-Za-z-\.]+))?$");
        var match = regex.Match(version.Trim());

        if (!match.Success)
        {
            return DefaultVersion;
        }

        if (!int.TryParse(match.Groups["major"].Value, out var major) || major < 0 ||
            !int.TryParse(match.Groups["minor"].Value, out var minor) || minor < 0 ||
            !int.TryParse(match.Groups["patch"].Value, out var patch) || patch < 0)
        {
            return DefaultVersion;
        }

        var prerelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null;
        var buildMetadata = match.Groups["buildmetadata"].Success ? match.Groups["buildmetadata"].Value : null;

        if (prerelease != null && !Regex.IsMatch(prerelease, @"^[0-9A-Za-z-\.]+$"))
        {
            prerelease = null;
        }

        if (buildMetadata != null && !Regex.IsMatch(buildMetadata, @"^[0-9A-Za-z-\.]+$"))
        {
            buildMetadata = null;
        }

        return new Version
        {
            Major = major,
            Minor = minor,
            Patch = patch,
            Prerelease = prerelease,
            BuildMetadata = buildMetadata
        };
    }

    /// <summary>
    /// Parses the version from an assembly's <see cref="AssemblyInformationalVersionAttribute"/>.
    /// Returns the default version (0.0.0) if the assembly or attribute is invalid.
    /// </summary>
    /// <param name="assembly">The assembly to extract the version from.</param>
    /// <returns>A <see cref="Version"/> object representing the assembly's version, or 0.0.0 if invalid.</returns>
    public static Version Parse(Assembly assembly)
    {
        if (assembly == null)
        {
            return DefaultVersion;
        }

        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attr == null || string.IsNullOrWhiteSpace(attr.InformationalVersion))
        {
            return DefaultVersion;
        }

        return Parse(attr.InformationalVersion);
    }

    /// <summary>
    /// Compares this version to another version according to Semantic Versioning precedence rules.
    /// </summary>
    /// <param name="other">The version to compare to.</param>
    /// <returns>
    /// A positive number if this version is greater, negative if less, or zero if equal.
    /// Build metadata is ignored in the comparison.
    /// </returns>
    public int CompareTo(Version other)
    {
        if (other == null)
        {
            return 1;
        }

        if (this.Major != other.Major)
        {
            return this.Major.CompareTo(other.Major);
        }

        if (this.Minor != other.Minor)
        {
            return this.Minor.CompareTo(other.Minor);
        }

        if (this.Patch != other.Patch)
        {
            return this.Patch.CompareTo(other.Patch);
        }

        if (string.IsNullOrEmpty(this.Prerelease) && string.IsNullOrEmpty(other.Prerelease))
        {
            return 0;
        }

        if (string.IsNullOrEmpty(this.Prerelease))
        {
            return 1;
        }

        if (string.IsNullOrEmpty(other.Prerelease))
        {
            return -1;
        }

        var thisParts = this.Prerelease.Split('.');
        var otherParts = other.Prerelease.Split('.');

        for (var i = 0; i < Math.Min(thisParts.Length, otherParts.Length); i++)
        {
            var thisPart = thisParts[i];
            var otherPart = otherParts[i];

            if (int.TryParse(thisPart, out var thisNum) && int.TryParse(otherPart, out var otherNum))
            {
                if (thisNum != otherNum)
                {
                    return thisNum.CompareTo(otherNum);
                }
            }
            else
            {
                var result = string.Compare(thisPart, otherPart, StringComparison.Ordinal);
                if (result != 0)
                {
                    return result;
                }
            }
        }

        return thisParts.Length.CompareTo(otherParts.Length);
    }

    /// <summary>
    /// Determines whether this version is equal to another version.
    /// </summary>
    /// <param name="other">The version to compare to.</param>
    /// <returns>True if the versions are equal, false otherwise.</returns>
    public bool Equals(Version other)
    {
        if (other == null)
        {
            return false;
        }

        return this.CompareTo(other) == 0;
    }

    /// <summary>
    /// Determines whether this version is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>True if the object is a <see cref="Version"/> and equal to this instance, false otherwise.</returns>
    public override bool Equals(object obj) => this.Equals(obj as Version);

    /// <summary>
    /// Gets the hash code for this version, based on major, minor, patch, and prerelease.
    /// </summary>
    /// <returns>The hash code for this version.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.Major, this.Minor, this.Patch, this.Prerelease);
    }

    /// <summary>
    /// Determines whether two versions are equal.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>True if the versions are equal, false otherwise.</returns>
    public static bool operator ==(Version a, Version b)
    {
        if (a is null)
        {
            return b is null;
        }

        return a.Equals(b);
    }

    /// <summary>
    /// Determines whether two versions are not equal.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>True if the versions are not equal, false otherwise.</returns>
    public static bool operator !=(Version a, Version b) => !(a == b);

    /// <summary>
    /// Determines whether the first version is less than the second.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>True if the first version is less than the second, false otherwise.</returns>
    public static bool operator <(Version a, Version b) => a?.CompareTo(b) < 0;

    /// <summary>
    /// Determines whether the first version is greater than the second.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>True if the first version is greater than the second, false otherwise.</returns>
    public static bool operator >(Version a, Version b) => a?.CompareTo(b) > 0;

    /// <summary>
    /// Determines whether the first version is less than or equal to the second.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>True if the first version is less than or equal to the second, false otherwise.</returns>
    public static bool operator <=(Version a, Version b) => a?.CompareTo(b) <= 0;

    /// <summary>
    /// Determines whether the first version is greater than or equal to the second.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>True if the first version is greater than or equal to the second, false otherwise.</returns>
    public static bool operator >=(Version a, Version b) => a?.CompareTo(b) >= 0;

    /// <summary>
    /// Renders the version string in the specified format.
    /// </summary>
    /// <param name="format">The format to use for rendering the version string.</param>
    /// <returns>The version string in the specified format.</returns>
    public string ToString(VersionFormat format)
    {
        var baseVersion = $"{Math.Max(0, this.Major)}.{Math.Max(0, this.Minor)}.{Math.Max(0, this.Patch)}";

        switch (format)
        {
            case VersionFormat.Short:
                return baseVersion;
            case VersionFormat.WithPrerelease:
                return !string.IsNullOrEmpty(this.Prerelease) ? $"{baseVersion}-{this.Prerelease}" : baseVersion;
            default:
                var version = !string.IsNullOrEmpty(this.Prerelease) ? $"{baseVersion}-{this.Prerelease}" : baseVersion;
                return !string.IsNullOrEmpty(this.BuildMetadata) ? $"{version}+{this.BuildMetadata}" : version;
        }
    }

    /// <summary>
    /// Renders the version string in the full format (major.minor.patch-prerelease+buildmetadata).
    /// </summary>
    /// <returns>The full version string.</returns>
    public override string ToString()
    {
        return this.ToString(VersionFormat.Full);
    }
}

/// <summary>
/// Defines the format for rendering a version string.
/// </summary>
public enum VersionFormat
{
    /// <summary>
    /// Renders the version as major.minor.patch (e.g., "1.0.0").
    /// </summary>
    Short,

    /// <summary>
    /// Renders the version as major.minor.patch-prerelease (e.g., "1.0.0-alpha").
    /// </summary>
    WithPrerelease,

    /// <summary>
    /// Renders the full version as major.minor.patch-prerelease+buildmetadata (e.g., "1.0.0-alpha+build123").
    /// </summary>
    Full
}

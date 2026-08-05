// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;

/// <summary>
///     Generates memorable lowercase names by combining an adjective with a noun.
/// </summary>
/// <example><code>var name = NameGenerator.Create(); // for example, "poisonivy"</code></example>
public static class NameGenerator
{
    private static readonly string[] Adjectives =
    [
        "agile",
        "amber",
        "ancient",
        "autumn",
        "azure",
        "bold",
        "brave",
        "bright",
        "calm",
        "clever",
        "cool",
        "cosmic",
        "crisp",
        "curious",
        "daring",
        "eager",
        "emerald",
        "fancy",
        "fast",
        "fierce",
        "gentle",
        "giant",
        "golden",
        "grand",
        "happy",
        "hidden",
        "icy",
        "jolly",
        "keen",
        "kind",
        "large",
        "lively",
        "lucky",
        "mellow",
        "mighty",
        "misty",
        "nimble",
        "noble",
        "patient",
        "playful",
        "poison",
        "proud",
        "quiet",
        "rapid",
        "red",
        "rugged",
        "silver",
        "small",
        "solar",
        "steady",
        "swift",
        "tiny",
        "vivid",
        "warm",
        "wild",
        "wise",
        "young"
    ];

    private static readonly string[] Nouns =
    [
        "ape",
        "badger",
        "bear",
        "beaver",
        "bison",
        "cedar",
        "cobra",
        "comet",
        "coral",
        "crane",
        "crow",
        "dolphin",
        "eagle",
        "falcon",
        "fern",
        "fox",
        "gecko",
        "heron",
        "ivy",
        "jaguar",
        "koala",
        "lark",
        "lynx",
        "maple",
        "moss",
        "otter",
        "owl",
        "panda",
        "pine",
        "puma",
        "raven",
        "reef",
        "robin",
        "shark",
        "sparrow",
        "spruce",
        "stag",
        "storm",
        "tiger",
        "turtle",
        "viper",
        "whale",
        "willow",
        "wolf",
        "yak",
        "zebra"
    ];

    /// <summary>
    ///     Creates a random, memorable name by joining a lowercase adjective and noun.
    /// </summary>
    /// <returns>A lowercase name containing only ASCII letters.</returns>
    /// <example><code>var name = NameGenerator.Create(); // for example, "poisonivy"</code></example>
    public static string Create()
    {
        var adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];

        return adjective + noun;
    }
}

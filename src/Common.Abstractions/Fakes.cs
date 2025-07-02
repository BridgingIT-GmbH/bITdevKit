// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;

public static class Fakes
{
    public static readonly FakeUser[] UsersStarwars =
    [
        new(
            "luke.skywalker@starwars.com",
            "Luke Skywalker",
            [Role.Administrators, Role.Users, Role.Readers, Role.Writers, Role.Contributors],
            "starwars",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3L3 10v11h18V10L12 3z"/>
                        <circle cx="12" cy="8" r="3"/>
                        <path d="M7 21v-4a5 5 0 0 1 10 0v4"/>
                    </svg>
                    """
                },
                {
                    "starwars", "yes"
                }
            },
            isDefault: true),
        new(
            "yoda@starwars.com",
            "Yoda",
            [Role.Administrators],
            "starwars",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3a5 5 0 0 0-5 5v2h10V8a5 5 0 0 0-5-5z"/>
                        <path d="M7 10v11h10V10"/>
                        <path d="M4 10h16"/>
                        <path d="M4 10l-2-2"/>
                        <path d="M20 10l2-2"/>
                    </svg>
                    """
                }
            }),
        new(
            "obi.wan@starwars.com",
            "Obi-Wan Kenobi",
            [Role.Administrators, Role.Users],
            "starwars",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="7" r="4"/>
                        <path d="M12 11v8"/>
                        <path d="M8 19h8"/>
                        <path d="M12 15l-2 2"/>
                        <path d="M12 15l2 2"/>
                    </svg>
                    """
                }
            }),
        new(
            "han.solo@starwars.com",
            "Han Solo",
            [Role.Administrators, Role.Contributors],
            "starwars",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3L4 9v12h16V9l-8-6z"/>
                        <path d="M8 15h8"/>
                        <path d="M12 11v8"/>
                        <circle cx="12" cy="7" r="2"/>
                    </svg>
                    """
                }
            }),
        new(
            "darth.vader@starwars.com",
            "Darth Vader",
            [Role.Administrators, Role.Users],
            "starwars",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3l-8 6v12h16V9l-8-6z"/>
                        <path d="M8 14h8"/>
                        <path d="M9 11l3 3 3-3"/>
                        <path d="M12 17v-3"/>
                    </svg>
                    """
                }
            }),
        //new(
        //    "chewbacca@starwars.com",
        //    "Chewbacca",
        //    [Role.Administrators],
        //    "starwars",
        //    new Dictionary<string, string>
        //    {
        //        {
        //            "avatar",
        //            """
        //            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        //                <circle cx="12" cy="8" r="5"/>
        //                <path d="M12 13v8"/>
        //                <path d="M9 17l3 3 3-3"/>
        //                <path d="M8 21h8"/>
        //            </svg>
        //            """
        //        }
        //    }),
        new(
            "anakin.skywalker@starwars.com",
            "Anakin Skywalker",
            [Role.Users, Role.Readers, Role.Writers],
            "starwars",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3L4 9v2h16V9L12 3z"/>
                        <path d="M4 11v10h16V11"/>
                        <path d="M9 15h6"/>
                        <path d="M12 13v4"/>
                    </svg>
                    """
                }
            }),
        //new(
        //    "r2d2@starwars.com",
        //    "R2-D2",
        //    [Role.Readers],
        //    "starwars",
        //    new Dictionary<string, string>
        //    {
        //        {
        //            "avatar",
        //            """
        //            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        //                <circle cx="12" cy="8" r="5"/>
        //                <rect x="8" y="13" width="8" height="8" rx="1"/>
        //                <circle cx="12" cy="8" r="2"/>
        //                <path d="M9 16h6"/>
        //                <path d="M9 18h6"/>
        //            </svg>
        //            """
        //        }
        //    }),
        //new(
        //    "c3po@starwars.com",
        //    "C-3PO",
        //    [Role.Writers],
        //    "starwars",
        //    new Dictionary<string, string>
        //    {
        //        {
        //            "avatar",
        //            """
        //            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        //                <circle cx="12" cy="7" r="4"/>
        //                <rect x="8" y="11" width="8" height="10" rx="2"/>
        //                <circle cx="10" cy="7" r="1" fill="currentColor"/>
        //                <circle cx="14" cy="7" r="1" fill="currentColor"/>
        //                <path d="M10 16h4"/>
        //            </svg>
        //            """
        //        }
        //    }),
        //new(
        //    "boba.fett@starwars.com",
        //    "Boba Fett",
        //    [Role.Guests],
        //    "starwars",
        //    new Dictionary<string, string>
        //    {
        //        {
        //            "avatar",
        //            """
        //            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        //                <path d="M12 3L4 9v2h16V9L12 3z"/>
        //                <path d="M4 11v10h16V11"/>
        //                <path d="M8 13h8"/>
        //                <path d="M6 15l12 0"/>
        //                <circle cx="12" cy="17" r="1"/>
        //            </svg>
        //            """
        //        }
        //    }),
        //new(
        //    "mace.windu@starwars.com",
        //    "Mace Windu",
        //    [Role.Contributors],
        //    "starwars",
        //    new Dictionary<string, string>
        //    {
        //        {
        //            "avatar",
        //            """
        //            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        //                <circle cx="12" cy="8" r="3"/>
        //                <path d="M7 21v-4a5 5 0 0 1 10 0v4"/>
        //                <path d="M3 21h18"/>
        //                <path d="M12 12v2"/>
        //                <path d="M10 14h4"/>
        //            </svg>
        //            """
        //        }
        //    })
    ];

    public static readonly FakeUser[] UsersGerman =
    [
        new(
            "johannes.schmidt@example.de",
            "Johannes Schmidt",
            [Role.Administrators, Role.Users, Role.Writers],
            "passwort",
            isDefault: true),
        new(
            "anna.mueller@example.de",
            "Anna Müller",
            [Role.Users, Role.Writers],
            "passwort"),
        new(
            "thomas.weber@example.de",
            "Thomas Weber",
            [Role.Readers, Role.Contributors],
            "passwort"),
        new(
            "elisabeth.fischer@example.de",
            "Elisabeth Fischer",
            [Role.Writers, Role.Contributors],
            "passwort"),
        new(
            "michael.wagner@example.de",
            "Michael Wagner",
            [Role.Users, Role.Readers],
            "passwort")
    ];

    public static readonly FakeUser[] UsersEnglish =
    [
       new(
            "john.smith@example.com",
            "John Smith",
            [Role.Administrators, Role.Users, Role.Writers],
            "password",
            isDefault: true),
        new(
            "anna.miller@example.com",
            "Anna Miller",
            [Role.Users, Role.Writers],
            "password"),
        new(
            "thomas.walker@example.com",
            "Thomas Walker",
            [Role.Readers, Role.Contributors],
            "password"),
        new(
            "elizabeth.fisher@example.com",
            "Elizabeth Fisher",
            [Role.Writers, Role.Contributors],
            "password"),
        new(
            "michael.warren@example.com",
            "Michael Warren",
            [Role.Users, Role.Readers],
            "password")
   ];
}

[DebuggerDisplay("Id={Id}, Email={Email}")]
public class FakeUser(string email, string name, string[] roles = null, string password = null, Dictionary<string, string> claims = null, bool isDefault = false)
{
    public string Id { get; } = GenerateDeterministicGuid(email);

    public bool IsEnabled { get; } = true;

    public bool IsDefault { get; } = isDefault;

    public string Email { get; } = email;

    public string Name { get; } = name;

    public string Password { get; } = password;

    public string[] Roles { get; } = roles;

    public IReadOnlyDictionary<string, string> Claims { get; } = claims ?? [];

    //private const string DefaultAvatar = """
    //    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
    //        <circle cx="12" cy="8" r="5"/>
    //        <path d="M3 21v-2a7 7 0 0 1 7-7h4a7 7 0 0 1 7 7v2"/>
    //    </svg>
    //    """;

    private static string GenerateDeterministicGuid(string value
)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash).ToString("N");
    }
}
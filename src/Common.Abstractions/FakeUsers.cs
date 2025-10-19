// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class FakeUsers
{
    public static readonly FakeUser[] Starwars =
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
            [Role.Users, Role.Readers, Role.Writers],
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
            [Role.Users],
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

    public static readonly FakeUser[] German =
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

    public static readonly FakeUser[] English =
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

    public static readonly FakeUser[] Fantasy =
    [
        new(
            "clever.dragon@example.com",
            "Clever Dragon",
            [Role.Administrators, Role.Users, Role.Readers, Role.Writers, Role.Contributors],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3L4 9v2h16V9L12 3z"/>
                        <path d="M4 11v10h16V11"/>
                        <path d="M8 15l4-2 4 2"/>
                        <circle cx="12" cy="17" r="2"/>
                    </svg>
                    """
                }
            },
            isDefault: true),
        new(
            "happy.phoenix@example.com",
            "Happy Phoenix",
            [Role.Administrators, Role.Writers, Role.Contributors],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3l4 4-4 4-4-4 4-4z"/>
                        <path d="M12 11v10"/>
                        <path d="M8 17l4 4 4-4"/>
                    </svg>
                    """
                }
            }),
        new(
            "eager.unicorn@example.com",
            "Eager Unicorn",
            [Role.Users, Role.Readers],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="8" r="4"/>
                        <path d="M12 4v4"/>
                        <path d="M12 12v9"/>
                        <path d="M8 17l4 4 4-4"/>
                    </svg>
                    """
                }
            }),
        new(
            "brave.griffin@example.com",
            "Brave Griffin",
            [Role.Writers, Role.Contributors],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3l6 6-6 6-6-6 6-6z"/>
                        <path d="M12 15v6"/>
                        <path d="M6 15l6 6 6-6"/>
                    </svg>
                    """
                }
            }),
        new(
            "jolly.wizard@example.com",
            "Jolly Wizard",
            [Role.Administrators, Role.Users],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3l-3 6h6l-3-6z"/>
                        <circle cx="12" cy="12" r="3"/>
                        <path d="M12 15v6"/>
                        <path d="M9 18h6"/>
                    </svg>
                    """
                }
            }),
        new(
            "peaceful.kraken@example.com",
            "Peaceful Kraken",
            [Role.Readers, Role.Contributors],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="8" r="5"/>
                        <path d="M8 13v8"/>
                        <path d="M12 13v8"/>
                        <path d="M16 13v8"/>
                        <path d="M6 17l2 2"/>
                        <path d="M18 17l-2 2"/>
                    </svg>
                    """
                }
            }),
        new(
            "sleepy.sphinx@example.com",
            "Sleepy Sphinx",
            [Role.Readers],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3a5 5 0 0 0-5 5v2h10V8a5 5 0 0 0-5-5z"/>
                        <rect x="7" y="10" width="10" height="11" rx="2"/>
                        <path d="M10 14h4"/>
                        <path d="M10 17h4"/>
                    </svg>
                    """
                }
            }),
        new(
            "quirky.gargoyle@example.com",
            "Quirky Gargoyle",
            [Role.Users, Role.Writers],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3L4 9v2h16V9L12 3z"/>
                        <path d="M4 11v10h16V11"/>
                        <circle cx="9" cy="14" r="1" fill="currentColor"/>
                        <circle cx="15" cy="14" r="1" fill="currentColor"/>
                        <path d="M9 18h6"/>
                    </svg>
                    """
                }
            }),
        new(
            "witty.centaur@example.com",
            "Witty Centaur",
            [Role.Contributors],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="7" r="4"/>
                        <path d="M12 11v4"/>
                        <path d="M7 15h10"/>
                        <path d="M8 15v6"/>
                        <path d="M16 15v6"/>
                    </svg>
                    """
                }
            }),
        new(
            "noble.pegasus@example.com",
            "Noble Pegasus",
            [Role.Guests],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="8" r="3"/>
                        <path d="M12 11v10"/>
                        <path d="M8 15l4 4 4-4"/>
                        <path d="M6 8l6-5"/>
                        <path d="M18 8l-6-5"/>
                    </svg>
                    """
                }
            }),
        new(
            "mighty.basilisk@example.com",
            "Mighty Basilisk",
            [Role.Administrators, Role.Readers],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 3c-3 0-5 2-5 5v3h10V8c0-3-2-5-5-5z"/>
                        <path d="M7 11v10h10V11"/>
                        <circle cx="10" cy="8" r="1" fill="currentColor"/>
                        <circle cx="14" cy="8" r="1" fill="currentColor"/>
                    </svg>
                    """
                }
            }),
        new(
            "swift.manticore@example.com",
            "Swift Manticore",
            [Role.Users, Role.Contributors],
            "fantasy",
            new Dictionary<string, string>
            {
                {
                    "avatar",
                    """
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="7" r="4"/>
                        <path d="M12 11v6"/>
                        <path d="M8 17l4 4 4-4"/>
                        <path d="M12 17l-4-2"/>
                        <path d="M12 17l4-2"/>
                    </svg>
                    """
                }
            })
    ];
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Common;

public static class CoreSeedModels
{
    public static IEnumerable<User> Users(long ticks) =>
        new[]
        {
            User.Create($"John{ticks}", $"Doe{ticks}", $"jdoe{ticks}@gmail.com", "H&%6EcTS"),
            User.Create($"Erik{ticks}", $"Larsson{ticks}", $"larsson{ticks}@gmail.com", "2x$nQ4gF"),
            User.Create($"Sophie{ticks}", $"Andersen{ticks}", $"sopa{ticks}@gmail.com", "39Xt5#*R"),
            User.Create($"Isabella{ticks}", $"Müller{ticks}", $"isam{ticks}@gmail.com", "K#%0A@vF"),
            User.Create($"Matthias{ticks}", $"Schmidt{ticks}", $"matties{ticks}@gmail.com", "etc12^0F"),
            User.Create($"Li{ticks}", $"Wei{ticks}", $"liwei{ticks}@gmail.com", "Yo26*G3Z"),
            User.Create($"Jane{ticks}", $"Smith{ticks}", $"js{ticks}@gmail.com", "i!c5k4OD"),
            User.Create($"Emily{ticks}", $"Johnson{ticks}", $"emjo{ticks}@gmail.com", "u*k*z8@Y")
        }.ForEach(u => u.Id = UserId.Create($"{GuidGenerator.Create($"User_{u.Email.Value}_{ticks}")}"));

    public static IEnumerable<Host> Hosts(long ticks) =>
        new[]
        {
            Host.Create($"John{ticks}", $"Doe{ticks}", UserId.Create(Users(ticks).ToArray()[0].Id.Value)),
            Host.Create($"Erik{ticks}", $"Larsson{ticks}", UserId.Create(Users(ticks).ToArray()[1].Id.Value)),
            Host.Create($"Sophie{ticks}", $"Andersen{ticks}", UserId.Create(Users(ticks).ToArray()[2].Id.Value)),
            Host.Create($"Isabella{ticks}", $"Müller{ticks}", UserId.Create(Users(ticks).ToArray()[3].Id.Value)),
            Host.Create($"Matthias{ticks}", $"Schmidt{ticks}", UserId.Create(Users(ticks).ToArray()[4].Id.Value)),
            Host.Create($"Li{ticks}", $"Wei{ticks}", UserId.Create(Users(ticks).ToArray()[5].Id.Value)),
            Host.Create($"Jane{ticks}", $"Smith{ticks}", UserId.Create(Users(ticks).ToArray()[6].Id.Value)),
            Host.Create($"Emily{ticks}", $"Johnson{ticks}", UserId.Create(Users(ticks).ToArray()[7].Id.Value))
        }.ForEach(h => h.Id = HostId.Create($"{GuidGenerator.Create($"Host_{h.UserId.Value}_{ticks}")}"));

    public static IEnumerable<Menu> Menus(long ticks) =>
        new[]
        {
            Menu.Create( // 0
                HostId.Create(Hosts(ticks).ToArray()[1].Id.Value), // Erik
                $"Vegetarian Delights {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Starters {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Roasted tomato soup with basil and croutons", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Bruschetta with tomatoes, basil, and balsamic vinegar", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Main course {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Spinach and ricotta stuffed shells", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Vegetable stir fry with rice or noodles", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Dessert {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Vegan cheesecake with a nut crust and cashew filling", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        })
                }),
            Menu.Create( // 1
                HostId.Create(Hosts(ticks).ToArray()[6].Id.Value), // Jane
                $"Spicey Delights {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Bold Bites {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Fire-roasted red pepper hummus with pita chips and vegetables", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Chipotle-spiced sweet potato fries with cilantro-lime aioli", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Fiery fusions {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Spicy Korean stir-fry with tofu and vegetables", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Grilled spicy shrimp skewers with pineapple salsa", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Jerk-spiced roasted vegetables with coconut rice\r\n", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Blaze a Trail {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Mango and chili sorbet with lime and mint", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Mexican hot chocolate with whipped cream and cinnamon", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        })
                }),
            Menu.Create( // 2
                HostId.Create(Hosts(ticks).ToArray()[6].Id.Value), // Jane
                $"A Coastal Culinary Voyage {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Tide-to-Table (Starters) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Coastal Crab Cakes with Spicy Remoulade", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Calamari Fritto Misto with Zesty Marinara Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Tuna Tartare with Avocado and Wasabi Aioli", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"From the Deep Blue (Main course) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Grilled Whole Red Snapper with Citrus Salsa", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Pan-Seared Sea Bass with Lemon Caper Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Siren's Sweetness (Dessert) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Lemon Blueberry Mascarpone Parfait", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Mango and Passionfruit Sorbet with Fresh Berries", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("White Chocolate Mousse with Raspberry Coulis", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                        })
                }),
            Menu.Create( // 3
                HostId.Create(Hosts(ticks).ToArray()[0].Id.Value), // John
                $"Flavors of East and West {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Oriental Delights (Starters) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Crispy Spring Rolls with Sweet Chili Dipping Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Thai Coconut Soup with Lemongrass and Galangal", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Cross-Cultural Connections (Main course) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Teriyaki Glazed Salmon with Sesame Stir-Fried Vegetables", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Thai Green Curry Chicken with Fragrant Jasmine Rice", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Vietnamese Lemongrass Tofu Banh Mi Sandwich", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Siren's Sweetness (Dessert) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Matcha Green Tea Tiramisu", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Yuzu Lemon Tart with Citrus Infusion", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        })
                }),
            Menu.Create( // 4
                HostId.Create(Hosts(ticks).ToArray()[7].Id.Value), // Emily
                $"A Carnivore's Culinary Journey {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Prime Cuts (Starters) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Classic Beef Carpaccio with Arugula and Parmesan", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Loaded Potato Skins with Bacon, Cheddar, and Sour Cream", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"From the Grill (Main course) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Dry-Aged Ribeye Steak with Red Wine Demi-Glace", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Grilled Filet Mignon with Béarnaise Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("T-Bone Steak with Smoky Barbecue Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Porterhouse Steak with Sautéed Mushrooms and Onions", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Sweet Indulgence (Dessert) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Caramelized Banana Foster with Whipped Cream", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Classic New York Cheesecake with Berry Compote", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Rich Chocolate Mousse with Fresh Berries", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        })
                }),
            Menu.Create( // 5
                HostId.Create(Hosts(ticks).ToArray()[4].Id.Value), // Matthias
                $"Prost & Pretzels {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Biergarten Bites (Starters) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Soft Pretzels with Mustard Dip", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Beer Cheese Soup with Pretzel Croutons", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Sauerkraut Fritters with Spicy Mustard", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Festive Feasts (Main course) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Traditional Bratwurst with Sauerkraut and German Potato Salad", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Sauerbraten (Braised Pot Roast) with Red Cabbage and Spaetzle", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Roasted Chicken with Beer Gravy, Mashed Potatoes, and Roasted Vegetables", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Sweet Endings (Dessert) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Apple Strudel with Vanilla Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Bavarian Cream Puffs with Chocolate Ganache", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        })
                }),
            Menu.Create( // 6
                HostId.Create(Hosts(ticks).ToArray()[3].Id.Value), // Isabella
                $"Berlin's Culinary Adventure {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                new[]
                {
                    MenuSection.Create(
                        $"Currywurst Corner (Starters) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Classic Currywurst with Spicy Curry Ketchup", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Bockwurst Bites with Mustard Dipping Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Kreuzberg Specials (Main course) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Currywurst with Fries and Curry Ketchup", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Kartoffelsuppe  with Sausage and Rye Bread", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        }),
                    MenuSection.Create(
                        $"Sweet Treats (Dessert) {ticks}",
                        "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
                        new[]
                        {
                            MenuSectionItem.Create("Apfelstrudel with Vanilla Sauce", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Schmalzkuchen with Powdered Sugar", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
                            MenuSectionItem.Create("Berliner with Jam Filling", "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
                        })
                }),
            Menu.Create( // 7
                HostId.Create(Hosts(ticks).ToArray()[0].Id.Value),
                $"Empty Menu {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et."),
            Menu.Create( // 8
                HostId.Create(Hosts(ticks).ToArray()[2].Id.Value),
                $"Autumn Menu {ticks}",
                "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.")
        }.ForEach(m => m.Id = MenuId.Create($"{GuidGenerator.Create($"Menu_{m.Name}_{ticks}")}"));

    public static IEnumerable<Dinner> Dinners(long ticks) =>
        new[]
        {
            Dinner.Create(
            $"Garden Delights: A Vegetarian Affair {ticks}",
            "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2023, 8, 26, 18, 30, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2023, 8, 26, 22, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "Art Otel",
                "Prins Hendrikkade 33",
                null,
                "1012 TM",
                "Amsterdam",
                "NL",
                "https://www.artotelamsterdam.com",
                52.377956,
                4.897070),
            true,
            9,
            MenuId.Create(Menus(ticks).ToArray()[0].Id.Value), // Vegetarian Delights
            HostId.Create(Hosts(ticks).ToArray()[1].Id.Value), // Erik
            Price.Create(23.99m, "EUR"),
            null),

            Dinner.Create(
            $"Seasonal Splendor: A Harvest Feast {ticks}",
            "Duo aliquyam sea aliquyam voluptua elitr eum et duo lorem adipiscing amet. Magna invidunt sanctus ex consectetuer aliquyam. Vero duo sed justo magna magna ex elitr stet lorem ut elitr accusam eirmod diam sed dolore sed magna. Accusam lorem et molestie sanctus sed luptatum et ipsum duis et.",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2023, 8, 26, 18, 30, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2023, 8, 26, 22, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "Café Restaurant Nieges",
                "Westerdoksdijk 40H",
                null,
                "1013 AE",
                "Amsterdam",
                "NL",
                "https://caferestaurantnieges.nl",
                52.387630,
                4.892670),
            true,
            9,
            MenuId.Create(Menus(ticks).ToArray()[8].Id.Value), // Autumn Menu
            HostId.Create(Hosts(ticks).ToArray()[2].Id.Value), // Sophie
            Price.Create(21.99m, "EUR"),
            null),

            Dinner.Create(
            $"Fire & Spice: A Fiery Fusion of Flavors {ticks}",
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed eget mauris non lacus aliquam molestie. Suspendisse eget fermentum justo. Sed convallis ultricies orci, ac hendrerit felis cursus at. Aliquam sodales quam at leo interdum tempus. Sed id tristique odio, a pellentesque ante. Ut vel blandit lacus, non interdum arcu. Morbi gravida tellus turpis, vitae vestibulum nibh euismod vel. ",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2023, 9, 10, 19, 0, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2023, 9, 10, 22, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "Spice House",
                "123 Main St",
                "Suite 100",
                "90210",
                "Beverly Hills",
                "USA",
                "https://www.spicehouse.com",
                34.069789,
                -118.400170),
            true,
            10,
            MenuId.Create(Menus(ticks).ToArray()[3].Id.Value), // Flavors of East and West
            HostId.Create(Hosts(ticks).ToArray()[0].Id.Value), // John
            Price.Create(34.99m, "USD"),
            null),

            Dinner.Create(
            $"Ocean's Bounty: A Seafood Extravaganza {ticks}",
            "Nunc ultrices, lectus id finibus vehicula, neque ipsum ullamcorper magna, in feugiat erat libero vitae odio. Curabitur venenatis faucibus odio, quis bibendum mauris laoreet at. Integer sed convallis tortor, eu finibus purus. Etiam ullamcorper convallis quam, vel malesuada nisi laoreet at. Sed quis ante dolor. ",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2023, 10, 5, 19, 30, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2023, 10, 5, 22, 30, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "The Crab Shack",
                "456 Ocean Blvd",
                null,
                "90265",
                "Malibu",
                "USA",
                "https://www.crabshack.com",
                34.037342,
                -118.689659),
            true,
            8,
            MenuId.Create(Menus(ticks).ToArray()[2].Id.Value), // A Coastal Culinary Voyage
            HostId.Create(Hosts(ticks).ToArray()[6].Id.Value), // Jane
            Price.Create(42.99m, "USD"),
            null),

            Dinner.Create(
            $"Asian Fusion: An Eclectic East-West Blend {ticks}",
            "Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Donec ac augue vel lorem sagittis ullamcorper eu vel sapien. Aliquam et nisl ut risus convallis consequat. Nam vel suscipit enim, in tincidunt nisi. In vel fermentum metus, eu laoreet lorem. Integer at consectetur urna, sed rutrum elit. ",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2023, 12, 3, 20, 0, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2023, 12, 3, 23, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "East Meets West",
                "456 Broadway",
                null,
                "10013",
                "New York",
                "USA",
                "https://www.eastmeetswest.com",
                40.721013,
                -74.000689),
            true,
            12,
            MenuId.Create(Menus(ticks).ToArray()[1].Id.Value), // Spicey Delights
            HostId.Create(Hosts(ticks).ToArray()[6].Id.Value), // Jane
            Price.Create(29.99m, "USD"),
            null),

            Dinner.Create(
            $"Steakhouse Classics: A Meat-Lover's Paradise {ticks}",
            "Suspendisse quis augue vel metus ultricies vestibulum. In sit amet pretium arcu, a posuere sapien. Proin et diam vitae lorem faucibus elementum quis eu magna. Maecenas fringilla massa a est convallis commodo. Aenean eget ante ut nulla commodo ullamcorper. Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas. ",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2024, 1, 14, 19, 0, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2024, 1, 14, 22, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "The Steakhouse",
                "789 Main St",
                null,
                "90210",
                "Beverly Hills",
                "USA",
                "https://www.steakhouse.com",
                34.068836,
                -118.413277),
            true,
            8,
            MenuId.Create(Menus(ticks).ToArray()[4].Id.Value), // A Carnivore's Culinary Journey
            HostId.Create(Hosts(ticks).ToArray()[7].Id.Value), // Emily
            Price.Create(49.99m, "USD"),
            null),

            Dinner.Create(
            $"Oktoberfest Feast: A Traditional German Dinner {ticks}",
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed suscipit eu urna vel ultrices. Maecenas consequat, justo a fringilla fringilla, quam quam mollis sapien, vel varius nisl felis in leo. Phasellus tempor sem ut volutpat luctus. Duis eu tristique arcu. Fusce bibendum dui id sem ultrices congue. Duis vel interdum dolor. ",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2024, 9, 21, 18, 0, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2024, 9, 21, 21, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "Hofbräuhaus",
                "Platzl 9",
                null,
                "80331",
                "Munich",
                "DE",
                "https://www.hofbraeuhaus.de/en",
                48.137529,
                11.580203),
            true,
            10,
            MenuId.Create(Menus(ticks).ToArray()[5].Id.Value), // Prost & Pretzels
            HostId.Create(Hosts(ticks).ToArray()[4].Id.Value), // Matthias
            Price.Create(39.99m, "EUR"),
            null),

            Dinner.Create(
            $"Berlin Street Food: A Culinary Tour of the City {ticks}",
            "Nulla commodo erat ac lorem consequat, vel dictum ipsum semper. Integer commodo fermentum tellus, vel viverra elit faucibus eget. In nec velit dolor. Maecenas aliquet ultrices tortor. In in libero semper, hendrerit nisi eu, sollicitudin ipsum. Vestibulum euismod ex ut odio imperdiet, at sollicitudin nulla bibendum. ",
            DinnerSchedule.Create(
                new DateTimeOffset(new DateTime(2024, 5, 15, 19, 0, 0, DateTimeKind.Utc)),
                new DateTimeOffset(new DateTime(2024, 5, 15, 22, 0, 0, DateTimeKind.Utc))),
            DinnerLocation.Create(
                "Street Food Market",
                "Markthalle Neun",
                "Eisenbahnstraße 42-43",
                "10997",
                "Berlin",
                "DE",
                "https://markthalleneun.de/en/street-food-thursday",
                52.499174,
                13.425495),
            true,
            15,
            MenuId.Create(Menus(ticks).ToArray()[6].Id.Value), // Berlin's Culinary Adventure
            HostId.Create(Hosts(ticks).ToArray()[3].Id.Value), // Isabella
            Price.Create(24.99m, "EUR"),
            null)
        }.ForEach(d => d.Id = DinnerId.Create($"{GuidGenerator.Create($"Dinner_{d.Name}_{ticks}")}"));
}

// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.Todos.Presentation.Web.Layout;

using MudBlazor;

public static class Themes
{
    public static readonly MudTheme Custom = new() // https://mudblazor.com/customization/overview#custom-themes
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#A12228", // red
            Secondary = "#A12228", // red
            Tertiary = "#3F6186",  // blue
            Error = "#A12228", // red
            Background = "#FFFFFF",
            TextPrimary = "#333333",
            TextSecondary = "#666666",
            Surface = "#FFFFFF"
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#A12228", // red
            Secondary = "#A12228", // red
            Tertiary = "#3F6186",  // blue
            Error = "#A12228", // red
            Background = "#333333",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#CCCCCC",
            Surface = "#424242"
        },
        Typography = new Typography()
        {
            H4 = new H4()
            {
                FontSize = "1.5rem",
                FontWeight = 500
            }
        },
        // Add specific button styles
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "0px" // Remove button border radius
        }
    };

    /*
    !
    * Bootswatch v5.3.3 (https://bootswatch.com)
    * Theme: cosmo
    * Copyright 2012-2024 Thomas Park
    * Licensed under MIT
    * Based on Bootstrap
    */
    public static readonly MudTheme Cosmo = new() // themes exported from https://themes.arctechonline.tech/
    {
        PaletteLight = new PaletteLight()
        {
            Black = "#000",
            White = "#fff",
            Primary = "#2780e3",
            PrimaryContrastText = "#d4e6f9",
            Secondary = "#373a3c",
            SecondaryContrastText = "#d7d8d8",
            Tertiary = "rgba(55, 58, 60, 0.5)",
            TertiaryContrastText = "#f8f9fa",
            Info = "#9954bb",
            InfoContrastText = "#ebddf1",
            Success = "#3fb618",
            SuccessContrastText = "#d9f0d1",
            Warning = "#ff7518",
            WarningContrastText = "#ffe3d1",
            Error = "rgba(244,67,54,1)",
            ErrorContrastText = "rgba(255,255,255,1)",
            Dark = "#373a3c",
            DarkContrastText = "#ced4da",
            TextPrimary = "#10335b",
            TextSecondary = "#161718",
            TextDisabled = "rgba(0,0,0,0.3764705882352941)",
            ActionDefault = "rgba(0,0,0,0.5372549019607843)",
            ActionDisabled = "rgba(0,0,0,0.25882352941176473)",
            ActionDisabledBackground = "rgba(0,0,0,0.11764705882352941)",
            Background = "rgba(255,255,255,1)",
            BackgroundGray = "rgba(245,245,245,1)",
            Surface = "rgba(255,255,255,1)",
            DrawerBackground = "#d7d8d8",
            DrawerText = "#373a3c",
            DrawerIcon = "#373a3c",
            AppbarBackground = "#d4e6f9",
            AppbarText = "#2780e3",
            LinesDefault = "rgba(0,0,0,0.11764705882352941)",
            LinesInputs = "rgba(189,189,189,1)",
            TableLines = "rgba(224,224,224,1)",
            TableStriped = "rgba(0,0,0,0.0196078431372549)",
            TableHover = "rgba(0,0,0,0.0392156862745098)",
            Divider = "rgba(224,224,224,1)",
            DividerLight = "rgba(0,0,0,0.8)",
            PrimaryDarken = "#10335b",
            PrimaryLighten = "#d4e6f9",
            SecondaryDarken = "#161718",
            SecondaryLighten = "#d7d8d8",
            TertiaryDarken = "rgba(55, 58, 60, 0.5)",
            TertiaryLighten = "rgba(55, 58, 60, 0.5)",
            InfoDarken = "#3d224b",
            InfoLighten = "#ebddf1",
            SuccessDarken = "#19490a",
            SuccessLighten = "#d9f0d1",
            WarningDarken = "#662f0a",
            WarningLighten = "#ffe3d1",
            ErrorDarken = "rgb(242,28,13)",
            ErrorLighten = "rgb(246,96,85)",
            DarkDarken = "rgb(46,46,46)",
            DarkLighten = "rgb(87,87,87)",
            HoverOpacity = 0.06,
            RippleOpacity = 0.1,
            RippleOpacitySecondary = 0.2,
            GrayDefault = "#9E9E9E",
            GrayLight = "#BDBDBD",
            GrayLighter = "#E0E0E0",
            GrayDark = "#757575",
            GrayDarker = "#616161",
            OverlayDark = "rgba(33,33,33,0.4980392156862745)",
            OverlayLight = "rgba(255,255,255,0.4980392156862745)",
        },
        PaletteDark = new PaletteDark()
        {
            Black = "#000",
            White = "#fff",
            Primary = "#2780e3",
            PrimaryContrastText = "#081a2d",
            Secondary = "#373a3c",
            SecondaryContrastText = "#0b0c0c",
            Tertiary = "rgba(222, 226, 230, 0.5)",
            TertiaryContrastText = "#2c3033",
            Info = "#9954bb",
            InfoContrastText = "#1f1125",
            Success = "#3fb618",
            SuccessContrastText = "#0d2405",
            Warning = "#ff7518",
            WarningContrastText = "#331705",
            Error = "rgba(244,67,54,1)",
            ErrorContrastText = "rgba(255,255,255,1)",
            Dark = "#373a3c",
            DarkContrastText = "#1c1d1e",
            TextPrimary = "#7db3ee",
            TextSecondary = "#87898a",
            TextDisabled = "rgba(255,255,255,0.2)",
            ActionDefault = "rgba(173,173,177,1)",
            ActionDisabled = "rgba(255,255,255,0.25882352941176473)",
            ActionDisabledBackground = "rgba(255,255,255,0.11764705882352941)",
            Background = "rgba(50,51,61,1)",
            BackgroundGray = "rgba(39,39,47,1)",
            Surface = "rgba(55,55,64,1)",
            DrawerBackground = "#0b0c0c",
            DrawerText = "#373a3c",
            DrawerIcon = "#373a3c",
            AppbarBackground = "#081a2d",
            AppbarText = "#2780e3",
            LinesDefault = "rgba(255,255,255,0.11764705882352941)",
            LinesInputs = "rgba(255,255,255,0.2980392156862745)",
            TableLines = "rgba(255,255,255,0.11764705882352941)",
            TableStriped = "rgba(255,255,255,0.2)",
            Divider = "rgba(255,255,255,0.11764705882352941)",
            DividerLight = "rgba(255,255,255,0.058823529411764705)",
            PrimaryDarken = "#7db3ee",
            PrimaryLighten = "#081a2d",
            SecondaryDarken = "#87898a",
            SecondaryLighten = "#0b0c0c",
            TertiaryDarken = "rgba(222, 226, 230, 0.5)",
            TertiaryLighten = "rgba(222, 226, 230, 0.5)",
            InfoDarken = "#c298d6",
            InfoLighten = "#1f1125",
            SuccessDarken = "#8cd374",
            SuccessLighten = "#0d2405",
            WarningDarken = "#ffac74",
            WarningLighten = "#331705",
            ErrorDarken = "rgb(242,28,13)",
            ErrorLighten = "rgb(246,96,85)",
            DarkDarken = "rgb(23,23,28)",
            DarkLighten = "rgb(56,56,67)",
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "4px",
            DrawerMiniWidthLeft = "56px",
            DrawerMiniWidthRight = "56px",
            DrawerWidthLeft = "240px",
            DrawerWidthRight = "240px",
            AppbarHeight = "64px",
        },
        //Typography = new Typography()
        //{
        //    Default = new DefaultTypography
        //    {
        //        FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"],
        //        FontWeight = "400",
        //        FontSize = ".875rem",
        //        LineHeight = "1.43",
        //        LetterSpacing = ".01071em",
        //        TextTransform = "none",
        //    },
        //    H1 = new H1Typography
        //    {
        //        FontWeight = "300",
        //        FontSize = "6rem",
        //        LineHeight = "1.167",
        //        LetterSpacing = "-.01562em",
        //        TextTransform = "none",
        //    },
        //    H2 = new H2Typography
        //    {
        //        FontWeight = "300",
        //        FontSize = "3.75rem",
        //        LineHeight = "1.2",
        //        LetterSpacing = "-.00833em",
        //        TextTransform = "none",
        //    },
        //    H3 = new H3Typography
        //    {
        //        FontWeight = "400",
        //        FontSize = "3rem",
        //        LineHeight = "1.167",
        //        LetterSpacing = "0",
        //        TextTransform = "none",
        //    },
        //    H4 = new H4Typography
        //    {
        //        FontWeight = "400",
        //        FontSize = "2.125rem",
        //        LineHeight = "1.235",
        //        LetterSpacing = ".00735em",
        //        TextTransform = "none",
        //    },
        //    H5 = new H5Typography
        //    {
        //        FontWeight = "400",
        //        FontSize = "1.5rem",
        //        LineHeight = "1.334",
        //        LetterSpacing = "0",
        //        TextTransform = "none",
        //    },
        //    H6 = new H6Typography
        //    {
        //        FontWeight = "500",
        //        FontSize = "1.25rem",
        //        LineHeight = "1.6",
        //        LetterSpacing = ".0075em",
        //        TextTransform = "none",
        //    },
        //    Subtitle1 = new Subtitle1Typography
        //    {
        //        FontWeight = "400",
        //        FontSize = "1rem",
        //        LineHeight = "1.75",
        //        LetterSpacing = ".00938em",
        //        TextTransform = "none",
        //    },
        //    Subtitle2 = new Subtitle2Typography
        //    {
        //        FontWeight = "500",
        //        FontSize = ".875rem",
        //        LineHeight = "1.57",
        //        LetterSpacing = ".00714em",
        //        TextTransform = "none",
        //    },
        //    Body1 = new Body1Typography
        //    {
        //        FontWeight = "400",
        //        FontSize = "1rem",
        //        LineHeight = "1.5",
        //        LetterSpacing = ".00938em",
        //        TextTransform = "none",
        //    },
        //    Body2 = new Body2Typography
        //    {
        //        FontWeight = "400",
        //        FontSize = ".875rem",
        //        LineHeight = "1.43",
        //        LetterSpacing = ".01071em",
        //        TextTransform = "none",
        //    },
        //    Button = new ButtonTypography
        //    {
        //        FontWeight = "500",
        //        FontSize = ".875rem",
        //        LineHeight = "1.75",
        //        LetterSpacing = ".02857em",
        //        TextTransform = "uppercase",
        //    },
        //    Caption = new CaptionTypography
        //    {
        //        FontWeight = "400",
        //        FontSize = ".75rem",
        //        LineHeight = "1.66",
        //        LetterSpacing = ".03333em",
        //        TextTransform = "none",
        //    },
        //    Overline = new OverlineTypography
        //    {
        //        FontWeight = "400",
        //        FontSize = ".75rem",
        //        LineHeight = "2.66",
        //        LetterSpacing = ".08333em",
        //        TextTransform = "none",
        //    },
        //},
        ZIndex = new ZIndex()
        {
            Drawer = 1100,
            Popover = 1200,
            AppBar = 1300,
            Dialog = 1400,
            Snackbar = 1500,
            Tooltip = 1600,
        },
    };
}
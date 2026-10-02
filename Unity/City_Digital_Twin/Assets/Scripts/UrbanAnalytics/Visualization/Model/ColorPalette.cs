using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    public enum ColorPaletteKind
    {
        /// <summary>Ordered low → high (one hue or lightness ramp).</summary>
        Sequential = 0,

        /// <summary>Two ramps meeting at a neutral midpoint.</summary>
        Diverging = 1,

        /// <summary>Unordered, distinct colours. Never interpolated.</summary>
        Categorical = 2
    }


    /// <summary>
    /// A named, immutable list of colour stops.
    ///
    /// Sequential and diverging palettes interpolate linearly
    /// (sRGB) between evenly spaced stops. Categorical palettes
    /// never blend: Evaluate snaps to the nearest stop.
    /// </summary>
    public sealed class ColorPalette
    {
        private readonly Color32[] stops;


        public string Id
        {
            get;
        }


        public string DisplayName
        {
            get;
        }


        public ColorPaletteKind Kind
        {
            get;
        }


        /// <summary>
        /// Where the colours come from (for attribution).
        /// </summary>
        public string Source
        {
            get;
        }


        public int StopCount =>
            stops.Length;


        public ColorPalette(
            string id,
            string displayName,
            ColorPaletteKind kind,
            string source,
            params string[] hexStops
        )
        {
            if (string.IsNullOrWhiteSpace(
                    id
                ))
            {
                throw new ArgumentException(
                    "Palette ID is required.",
                    nameof(id)
                );
            }


            if (hexStops == null ||
                hexStops.Length < 2)
            {
                throw new ArgumentException(
                    "A palette needs at least two colours.",
                    nameof(hexStops)
                );
            }


            Id =
                id;

            DisplayName =
                displayName;

            Kind =
                kind;

            Source =
                source;


            stops =
                new Color32[
                    hexStops.Length
                ];


            for (
                int i = 0;
                i < hexStops.Length;
                i++
            )
            {
                if (!ColorUtility.TryParseHtmlString(
                        hexStops[i],
                        out Color color
                    ))
                {
                    throw new ArgumentException(
                        $"Palette '{id}' has an invalid " +
                        $"colour '{hexStops[i]}'.",
                        nameof(hexStops)
                    );
                }


                stops[i] =
                    color;
            }
        }


        public Color32 GetStop(
            int index
        )
        {
            return stops[
                Mathf.Clamp(
                    index,
                    0,
                    stops.Length - 1
                )
            ];
        }


        /// <summary>
        /// Colour for a category index. Wraps around when there
        /// are more categories than colours.
        /// </summary>
        public Color32 GetCategory(
            int index
        )
        {
            int wrapped =
                ((index % stops.Length) + stops.Length) %
                stops.Length;


            return stops[
                wrapped
            ];
        }


        /// <summary>
        /// t in [0, 1]. Categorical palettes snap to the nearest
        /// stop; the others interpolate.
        /// </summary>
        public Color32 Evaluate(
            float t
        )
        {
            t =
                Mathf.Clamp01(
                    t
                );


            float position =
                t * (stops.Length - 1);


            if (Kind ==
                ColorPaletteKind.Categorical)
            {
                return stops[
                    Mathf.RoundToInt(
                        position
                    )
                ];
            }


            int lower =
                Mathf.Min(
                    Mathf.FloorToInt(
                        position
                    ),
                    stops.Length - 2
                );


            return Color32.Lerp(
                stops[lower],
                stops[lower + 1],
                position - lower
            );
        }
    }


    /// <summary>
    /// Built-in palettes, addressed by string ID so they can be
    /// referenced from Inspector fields and, later, from external
    /// scenario files.
    ///
    /// Sources:
    /// - ColorBrewer 2.0 (Cynthia Brewer, Penn State), Apache 2.0
    ///   licence: blues, ylorrd, ylgnbu, rdbu, brbg, puor, set2.
    /// - matplotlib (van der Walt &amp; Smith), CC0: viridis, magma,
    ///   sampled at 9 evenly spaced points.
    /// - Tableau 10 (Tableau Software): tableau10.
    /// - Okabe &amp; Ito (2008) colour-blind-safe set: okabe_ito.
    /// </summary>
    public static class ColorPaletteLibrary
    {
        private static readonly Dictionary<string, ColorPalette>
            palettes =
                new Dictionary<string, ColorPalette>(
                    StringComparer.OrdinalIgnoreCase
                );


        static ColorPaletteLibrary()
        {
            // ---------------- Sequential ----------------

            Add(
                new ColorPalette(
                    "viridis",
                    "Viridis",
                    ColorPaletteKind.Sequential,
                    "matplotlib",
                    "#440154", "#472d7b", "#3b528b", "#2c728e", "#21918c",
                    "#28ae80", "#5ec962", "#addc30", "#fde725"
                )
            );

            Add(
                new ColorPalette(
                    "magma",
                    "Magma",
                    ColorPaletteKind.Sequential,
                    "matplotlib",
                    "#000004", "#1c1044", "#4f127b", "#812581", "#b5367a",
                    "#e55964", "#fb8761", "#fec287", "#fcfdbf"
                )
            );

            Add(
                new ColorPalette(
                    "blues",
                    "Blues",
                    ColorPaletteKind.Sequential,
                    "ColorBrewer",
                    "#f7fbff", "#deebf7", "#c6dbef", "#9ecae1", "#6baed6",
                    "#4292c6", "#2171b5", "#08519c", "#08306b"
                )
            );

            Add(
                new ColorPalette(
                    "ylorrd",
                    "Yellow-Orange-Red",
                    ColorPaletteKind.Sequential,
                    "ColorBrewer",
                    "#ffffcc", "#ffeda0", "#fed976", "#feb24c", "#fd8d3c",
                    "#fc4e2a", "#e31a1c", "#bd0026", "#800026"
                )
            );

            Add(
                new ColorPalette(
                    "ylgnbu",
                    "Yellow-Green-Blue",
                    ColorPaletteKind.Sequential,
                    "ColorBrewer",
                    "#ffffd9", "#edf8b1", "#c7e9b4", "#7fcdbb", "#41b6c4",
                    "#1d91c0", "#225ea8", "#253494", "#081d58"
                )
            );

            // ---------------- Diverging ----------------

            Add(
                new ColorPalette(
                    "rdbu",
                    "Red-Blue",
                    ColorPaletteKind.Diverging,
                    "ColorBrewer",
                    "#67001f", "#b2182b", "#d6604d", "#f4a582", "#fddbc7",
                    "#f7f7f7",
                    "#d1e5f0", "#92c5de", "#4393c3", "#2166ac", "#053061"
                )
            );

            Add(
                new ColorPalette(
                    "brbg",
                    "Brown-Blue-Green",
                    ColorPaletteKind.Diverging,
                    "ColorBrewer",
                    "#543005", "#8c510a", "#bf812d", "#dfc27d", "#f6e8c3",
                    "#f5f5f5",
                    "#c7eae5", "#80cdc1", "#35978f", "#01665e", "#003c30"
                )
            );

            Add(
                new ColorPalette(
                    "puor",
                    "Orange-Purple",
                    ColorPaletteKind.Diverging,
                    "ColorBrewer",
                    "#7f3b08", "#b35806", "#e08214", "#fdb863", "#fee0b6",
                    "#f7f7f7",
                    "#d8daeb", "#b2abd2", "#8073ac", "#542788", "#2d004b"
                )
            );

            // ---------------- Categorical ----------------

            Add(
                new ColorPalette(
                    "tableau10",
                    "Tableau 10",
                    ColorPaletteKind.Categorical,
                    "Tableau",
                    "#4e79a7", "#f28e2b", "#e15759", "#76b7b2", "#59a14f",
                    "#edc948", "#b07aa1", "#ff9da7", "#9c755f", "#bab0ac"
                )
            );

            Add(
                new ColorPalette(
                    "set2",
                    "Set 2",
                    ColorPaletteKind.Categorical,
                    "ColorBrewer",
                    "#66c2a5", "#fc8d62", "#8da0cb", "#e78ac3", "#a6d854",
                    "#ffd92f", "#e5c494", "#b3b3b3"
                )
            );

            Add(
                new ColorPalette(
                    "okabe_ito",
                    "Okabe-Ito (colour-blind safe)",
                    ColorPaletteKind.Categorical,
                    "Okabe & Ito 2008",
                    "#e69f00", "#56b4e9", "#009e73", "#f0e442", "#0072b2",
                    "#d55e00", "#cc79a7", "#000000"
                )
            );
        }


        public static IEnumerable<ColorPalette> All =>
            palettes.Values;


        /// <summary>
        /// Returns a warning when a palette is a poor match for a
        /// scale type, or null when the pairing is sound.
        /// </summary>
        public static string CheckCompatibility(
            ColorPalette palette,
            ScaleType scaleType
        )
        {
            if (palette == null)
            {
                return null;
            }


            switch (palette.Kind)
            {
                case ColorPaletteKind.Categorical:
                    return scaleType == ScaleType.Quantile
                        ? $"Categorical palette '{palette.Id}' on " +
                          $"ordered quantile classes: the colours " +
                          $"do not show which class is higher. " +
                          $"Prefer a sequential palette."
                        : $"Categorical palette '{palette.Id}' on a " +
                          $"continuous {scaleType} scale snaps " +
                          $"values to {palette.StopCount} unordered " +
                          $"colours. Prefer a sequential palette.";

                case ColorPaletteKind.Diverging:
                    return scaleType == ScaleType.Diverging
                        ? null
                        : $"Diverging palette '{palette.Id}' on a " +
                          $"{scaleType} scale: its neutral midpoint " +
                          $"has no meaning without a centre value. " +
                          $"Use a Diverging scale or a sequential " +
                          $"palette.";

                default:
                    return scaleType == ScaleType.Diverging
                        ? $"Sequential palette '{palette.Id}' on a " +
                          $"Diverging scale: values above and below " +
                          $"the centre are not distinguishable by " +
                          $"hue. Prefer a diverging palette."
                        : null;
            }
        }


        public static bool TryGet(
            string id,
            out ColorPalette palette
        )
        {
            palette =
                null;


            return
                !string.IsNullOrWhiteSpace(
                    id
                ) &&
                palettes.TryGetValue(
                    id.Trim(),
                    out palette
                );
        }


        private static void Add(
            ColorPalette palette
        )
        {
            palettes.Add(
                palette.Id,
                palette
            );
        }
    }
}

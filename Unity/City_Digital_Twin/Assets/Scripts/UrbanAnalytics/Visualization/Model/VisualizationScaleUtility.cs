using System;
using System.Globalization;
using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// A scale resolved against actual data: maps a value to
    /// [0, 1] for colour or height encodings.
    /// </summary>
    public readonly struct ResolvedNumericScale
    {
        private readonly double[] breaks;


        /// <summary>
        /// Low end of the input range. For Diverging this is
        /// centre - half-extent (the range is symmetric).
        /// </summary>
        public double Minimum
        {
            get;
        }

        public double Maximum
        {
            get;
        }

        public ScaleType Type
        {
            get;
        }

        /// <summary>Diverging only.</summary>
        public double Center
        {
            get;
        }

        /// <summary>Quantile only: number of classes.</summary>
        public int ClassCount
        {
            get;
        }

        /// <summary>
        /// Quantile only: the N - 1 inner class boundaries,
        /// ascending. Class i holds breaks[i-1] ≤ v &lt; breaks[i].
        /// </summary>
        public double[] Breaks =>
            breaks != null
                ? (double[])breaks.Clone()
                : Array.Empty<double>();

        /// <summary>Valid values below the range (clamped).</summary>
        public int ClampedLow
        {
            get;
        }

        /// <summary>Valid values above the range (clamped).</summary>
        public int ClampedHigh
        {
            get;
        }

        /// <summary>Short human-readable description for legends.</summary>
        public string Description
        {
            get;
        }


        public ResolvedNumericScale(
            double minimum,
            double maximum
        )
            : this(
                ScaleType.Linear,
                minimum,
                maximum,
                0.0,
                null,
                0,
                0,
                "linear"
            )
        {
        }


        public ResolvedNumericScale(
            ScaleType type,
            double minimum,
            double maximum,
            double center,
            double[] breaks,
            int clampedLow,
            int clampedHigh,
            string description
        )
        {
            Type =
                type;

            Minimum =
                minimum;

            Maximum =
                maximum;

            Center =
                center;

            this.breaks =
                breaks;

            ClassCount =
                breaks != null
                    ? breaks.Length + 1
                    : 0;

            ClampedLow =
                clampedLow;

            ClampedHigh =
                clampedHigh;

            Description =
                description ?? string.Empty;
        }


        public float Normalize(
            double value
        )
        {
            if (double.IsNaN(
                    value
                ))
            {
                return 0.0f;
            }


            double normalized;


            switch (Type)
            {
                case ScaleType.Log:
                    {
                        if (value <= 0.0)
                        {
                            return 0.0f;
                        }


                        double low =
                            Math.Log10(
                                Minimum
                            );

                        double high =
                            Math.Log10(
                                Maximum
                            );


                        if (high <= low)
                        {
                            return 0.5f;
                        }


                        normalized =
                            (Math.Log10(value) - low) /
                            (high - low);

                        break;
                    }


                case ScaleType.Diverging:
                    {
                        double half =
                            (Maximum - Minimum) * 0.5;


                        if (half <= 0.0)
                        {
                            return 0.5f;
                        }


                        normalized =
                            0.5 +
                            0.5 * (value - Center) / half;

                        break;
                    }


                case ScaleType.Quantile:
                    {
                        return ClassToNormalized(
                            ClassOf(
                                value
                            )
                        );
                    }


                default:
                    {
                        if (Maximum <= Minimum)
                        {
                            return 0.5f;
                        }


                        normalized =
                            (value - Minimum) /
                            (Maximum - Minimum);

                        break;
                    }
            }


            return (float)Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    normalized
                )
            );
        }


        /// <summary>
        /// Maps a legend position x in [0, 1] to the normalized
        /// value whose colour should be drawn there. Identity
        /// for continuous scales; stepped for Quantile, so the
        /// legend shows exactly the class colours used.
        /// </summary>
        public float LegendPositionToNormalized(
            float x
        )
        {
            if (Type != ScaleType.Quantile ||
                ClassCount < 2)
            {
                return x;
            }


            int classIndex =
                Math.Min(
                    ClassCount - 1,
                    (int)Math.Floor(
                        Math.Max(0.0f, x) * ClassCount
                    )
                );


            return ClassToNormalized(
                classIndex
            );
        }


        /// <summary>Quantile only: class index 0..N-1 of a value.</summary>
        public int ClassOf(
            double value
        )
        {
            if (breaks == null)
            {
                return 0;
            }


            int classIndex =
                0;


            while (classIndex < breaks.Length &&
                   value >= breaks[classIndex])
            {
                classIndex++;
            }


            return classIndex;
        }


        private float ClassToNormalized(
            int classIndex
        )
        {
            return ClassCount < 2
                ? 0.5f
                : classIndex / (float)(ClassCount - 1);
        }
    }


    public static class VisualizationScaleUtility
    {
        public static ResolvedNumericScale Resolve(
            DataLayer dataLayer,
            string variableId,
            NumericScaleSpec spec
        )
        {
            if (dataLayer == null)
            {
                throw new ArgumentNullException(
                    nameof(dataLayer)
                );
            }

            if (string.IsNullOrWhiteSpace(
                    variableId
                ))
            {
                throw new ArgumentException(
                    "Variable ID cannot be empty.",
                    nameof(variableId)
                );
            }


            if (!dataLayer.TryGetNumericValues(
                    variableId,
                    out double[] values
                ))
            {
                throw new InvalidOperationException(
                    $"'{dataLayer.Id}.{variableId}' is not a " +
                    $"numeric variable."
                );
            }


            if (values.Length == 0)
            {
                throw new InvalidOperationException(
                    $"'{dataLayer.Id}.{variableId}' has no valid " +
                    $"values to build a scale from."
                );
            }


            return Resolve(
                values,
                spec
            );
        }


        /// <summary>
        /// Resolves a scale from raw values. Non-finite values
        /// are ignored. Pure function (no Unity / data-layer
        /// dependencies) so it can be unit-tested directly.
        /// </summary>
        public static ResolvedNumericScale Resolve(
            double[] values,
            NumericScaleSpec spec
        )
        {
            return Resolve(
                values,
                spec,
                false
            );
        }


        /// <summary>
        /// includeZero: for Linear scales, extend the domain to
        /// contain 0 so mark lengths are proportional to values
        /// (used by bar-like glyphs). No effect on other types.
        /// </summary>
        public static ResolvedNumericScale Resolve(
            double[] values,
            NumericScaleSpec spec,
            bool includeZero
        )
        {
            if (values == null)
            {
                throw new ArgumentNullException(
                    nameof(values)
                );
            }

            if (spec == null)
            {
                throw new ArgumentNullException(
                    nameof(spec)
                );
            }


            double[] sorted =
                Array.FindAll(
                    values,
                    v => !double.IsNaN(v) &&
                         !double.IsInfinity(v)
                );


            if (sorted.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cannot build a scale: no finite values."
                );
            }


            Array.Sort(
                sorted
            );


            if (spec.Type == ScaleType.Quantile)
            {
                return ResolveQuantile(
                    sorted,
                    spec
                );
            }


            ResolveDomain(
                sorted,
                spec,
                out double minimum,
                out double maximum,
                out string domainText
            );


            double center =
                0.0;

            string description;


            switch (spec.Type)
            {
                case ScaleType.Log:
                    {
                        if (maximum <= 0.0)
                        {
                            throw new InvalidOperationException(
                                "Log scale needs a positive " +
                                "maximum."
                            );
                        }


                        if (minimum <= 0.0)
                        {
                            if (spec.DomainMode ==
                                ScaleDomainMode.Manual)
                            {
                                throw new InvalidOperationException(
                                    "Log scale needs a positive " +
                                    "manual minimum."
                                );
                            }


                            // Data-driven domain: start at the
                            // smallest positive value instead.
                            minimum =
                                SmallestPositive(
                                    sorted
                                );
                        }


                        description =
                            "log scale, " + domainText;

                        break;
                    }


                case ScaleType.Diverging:
                    {
                        center =
                            spec.DivergingCenterMode ==
                            DivergingCenterMode.DataMedian
                                ? Percentile(
                                    sorted,
                                    50.0
                                )
                                : spec.DivergingCenter;


                        if (double.IsNaN(center) ||
                            double.IsInfinity(center))
                        {
                            throw new InvalidOperationException(
                                "Diverging centre is not finite."
                            );
                        }


                        double half =
                            Math.Max(
                                center - minimum,
                                maximum - center
                            );


                        minimum =
                            center - half;

                        maximum =
                            center + half;


                        description =
                            "diverging around " +
                            FormatValue(center) +
                            (spec.DivergingCenterMode ==
                             DivergingCenterMode.DataMedian
                                ? " (median)"
                                : string.Empty) +
                            ", " +
                            domainText;

                        break;
                    }


                default:
                    {
                        description =
                            domainText;


                        if (includeZero &&
                            (minimum > 0.0 || maximum < 0.0))
                        {
                            minimum =
                                Math.Min(
                                    minimum,
                                    0.0
                                );

                            maximum =
                                Math.Max(
                                    maximum,
                                    0.0
                                );

                            description +=
                                ", from zero";
                        }

                        break;
                    }
            }


            if (maximum < minimum)
            {
                throw new InvalidOperationException(
                    $"Visualization scale maximum ({maximum}) " +
                    $"is smaller than minimum ({minimum})."
                );
            }


            CountClamped(
                sorted,
                spec.Type == ScaleType.Log
                    ? Math.Max(minimum, double.Epsilon)
                    : minimum,
                maximum,
                out int low,
                out int high
            );


            return new ResolvedNumericScale(
                spec.Type,
                minimum,
                maximum,
                center,
                null,
                low,
                high,
                description
            );
        }


        /// <summary>
        /// Linear-interpolated percentile (same definition as
        /// numpy.percentile's default). p in [0, 100].
        /// </summary>
        public static double Percentile(
            double[] sortedValues,
            double p
        )
        {
            if (sortedValues == null ||
                sortedValues.Length == 0)
            {
                throw new ArgumentException(
                    "No values.",
                    nameof(sortedValues)
                );
            }


            double rank =
                Math.Max(0.0, Math.Min(100.0, p)) / 100.0 *
                (sortedValues.Length - 1);


            int lower =
                (int)Math.Floor(
                    rank
                );

            int upper =
                Math.Min(
                    lower + 1,
                    sortedValues.Length - 1
                );


            return sortedValues[lower] +
                   (sortedValues[upper] - sortedValues[lower]) *
                   (rank - lower);
        }


        // =========================================================
        // INTERNAL
        // =========================================================

        private static void ResolveDomain(
            double[] sorted,
            NumericScaleSpec spec,
            out double minimum,
            out double maximum,
            out string text
        )
        {
            switch (spec.DomainMode)
            {
                case ScaleDomainMode.DataMinMax:
                    {
                        minimum =
                            sorted[0];

                        maximum =
                            sorted[sorted.Length - 1];

                        text =
                            "data min-max";

                        break;
                    }


                case ScaleDomainMode.Manual:
                    {
                        minimum =
                            spec.ManualMinimum;

                        maximum =
                            spec.ManualMaximum;

                        text =
                            "manual range";

                        break;
                    }


                case ScaleDomainMode.Percentile:
                    {
                        double lowerP =
                            spec.LowerPercentile;

                        double upperP =
                            spec.UpperPercentile;


                        if (lowerP < 0.0 ||
                            upperP > 100.0 ||
                            lowerP >= upperP)
                        {
                            throw new InvalidOperationException(
                                $"Invalid percentile domain " +
                                $"[{lowerP}, {upperP}]: needs " +
                                $"0 <= lower < upper <= 100."
                            );
                        }


                        minimum =
                            Percentile(
                                sorted,
                                lowerP
                            );

                        maximum =
                            Percentile(
                                sorted,
                                upperP
                            );

                        text =
                            FormatPercentile(lowerP) +
                            "-" +
                            FormatPercentile(upperP) +
                            " percentile";

                        break;
                    }


                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(spec.DomainMode),
                        spec.DomainMode,
                        "Unknown scale domain mode."
                    );
            }


            if (double.IsNaN(minimum) ||
                double.IsInfinity(minimum) ||
                double.IsNaN(maximum) ||
                double.IsInfinity(maximum))
            {
                throw new InvalidOperationException(
                    "Visualization scale contains non-finite values."
                );
            }
        }


        private static ResolvedNumericScale ResolveQuantile(
            double[] sorted,
            NumericScaleSpec spec
        )
        {
            int classes =
                spec.QuantileClasses;


            if (classes < 2 ||
                classes > 12)
            {
                throw new InvalidOperationException(
                    $"Quantile scale needs 2-12 classes, " +
                    $"got {classes}."
                );
            }


            var breaks =
                new double[
                    classes - 1
                ];


            for (
                int k = 1;
                k < classes;
                k++
            )
            {
                breaks[k - 1] =
                    Percentile(
                        sorted,
                        100.0 * k / classes
                    );
            }


            return new ResolvedNumericScale(
                ScaleType.Quantile,
                sorted[0],
                sorted[sorted.Length - 1],
                0.0,
                breaks,
                0,
                0,
                $"{classes} quantile classes"
            );
        }


        private static double SmallestPositive(
            double[] sorted
        )
        {
            foreach (double value in sorted)
            {
                if (value > 0.0)
                {
                    return value;
                }
            }


            throw new InvalidOperationException(
                "Log scale needs at least one positive value."
            );
        }


        private static void CountClamped(
            double[] sorted,
            double minimum,
            double maximum,
            out int low,
            out int high
        )
        {
            low =
                0;

            high =
                0;


            foreach (double value in sorted)
            {
                if (value < minimum)
                {
                    low++;
                }
                else if (value > maximum)
                {
                    high++;
                }
            }
        }


        private static string FormatPercentile(
            double p
        )
        {
            return p.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            );
        }


        private static string FormatValue(
            double value
        )
        {
            return value.ToString(
                "0.###",
                CultureInfo.InvariantCulture
            );
        }
    }
}

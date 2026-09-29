using System;
using UrbanAnalytics.Data;

namespace UrbanAnalytics.Visualization
{
    public readonly struct ResolvedNumericScale
    {
        public double Minimum
        {
            get;
        }

        public double Maximum
        {
            get;
        }


        public ResolvedNumericScale(
            double minimum,
            double maximum
        )
        {
            Minimum =
                minimum;

            Maximum =
                maximum;
        }


        public float Normalize(
            double value
        )
        {
            if (Maximum <= Minimum)
            {
                return 0.5f;
            }

            double normalized =
                (value - Minimum) /
                (Maximum - Minimum);

            if (normalized < 0.0)
            {
                normalized = 0.0;
            }
            else if (normalized > 1.0)
            {
                normalized = 1.0;
            }

            return (float)normalized;
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

            if (spec == null)
            {
                throw new ArgumentNullException(
                    nameof(spec)
                );
            }


            double minimum;
            double maximum;


            switch (spec.DomainMode)
            {
                case ScaleDomainMode.DataMinMax:
                    {
                        if (!dataLayer.TryGetNumericRange(
                                variableId,
                                out minimum,
                                out maximum
                            ))
                        {
                            throw new InvalidOperationException(
                                $"Could not resolve numeric range for " +
                                $"'{dataLayer.Id}.{variableId}'."
                            );
                        }

                        break;
                    }


                case ScaleDomainMode.Manual:
                    {
                        minimum =
                            spec.ManualMinimum;

                        maximum =
                            spec.ManualMaximum;

                        break;
                    }


                default:
                    throw new ArgumentOutOfRangeException();
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


            if (maximum < minimum)
            {
                throw new InvalidOperationException(
                    $"Visualization scale maximum ({maximum}) " +
                    $"is smaller than minimum ({minimum})."
                );
            }


            return new ResolvedNumericScale(
                minimum,
                maximum
            );
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.Spatial
{
    /// <summary>
    /// Runtime representation of one fully loaded spatial layer.
    ///
    /// Contains:
    /// - the validated layer definition;
    /// - all semantic SpatialUnits;
    /// - stable lookup by unit ID;
    /// - aggregate source-CRS bounds.
    ///
    /// It does not contain:
    /// - analytical data values;
    /// - Unity GameObjects;
    /// - rendering state;
    /// - interaction state.
    /// </summary>
    public sealed class SpatialLayer
    {
        private readonly List<SpatialUnit> units;
        private readonly Dictionary<string, SpatialUnit> unitsById;

        public SpatialLayerDefinition Definition
        {
            get;
        }

        public string Id =>
            Definition.Id;

        public string DisplayName =>
            Definition.DisplayName;

        public IReadOnlyList<SpatialUnit> Units
        {
            get;
        }

        public int UnitCount =>
            units.Count;

        public SpatialBounds Bounds
        {
            get;
        }


        public SpatialLayer(
            SpatialLayerDefinition definition,
            IEnumerable<SpatialUnit> units
        )
        {
            Definition =
                definition
                ?? throw new ArgumentNullException(
                    nameof(definition)
                );

            if (units == null)
            {
                throw new ArgumentNullException(
                    nameof(units)
                );
            }

            this.units =
                new List<SpatialUnit>();

            unitsById =
                new Dictionary<string, SpatialUnit>(
                    StringComparer.Ordinal
                );

            foreach (SpatialUnit unit in units)
            {
                if (unit == null)
                {
                    throw new ArgumentException(
                        "Spatial layer cannot contain null units.",
                        nameof(units)
                    );
                }

                if (!string.Equals(
                        unit.SpatialLayerId,
                        definition.Id,
                        StringComparison.Ordinal
                    ))
                {
                    throw new ArgumentException(
                        $"Spatial unit '{unit.Id}' belongs to " +
                        $"layer '{unit.SpatialLayerId}', but is being " +
                        $"added to layer '{definition.Id}'."
                    );
                }

                if (!unitsById.TryAdd(
                        unit.Id,
                        unit
                    ))
                {
                    throw new ArgumentException(
                        $"Duplicate spatial unit ID '{unit.Id}' " +
                        $"in layer '{definition.Id}'."
                    );
                }

                this.units.Add(
                    unit
                );
            }

            if (this.units.Count == 0)
            {
                throw new ArgumentException(
                    $"Spatial layer '{definition.Id}' contains no units.",
                    nameof(units)
                );
            }

            if (definition.UnitCount !=
                this.units.Count)
            {
                throw new ArgumentException(
                    $"Spatial layer '{definition.Id}' expected " +
                    $"{definition.UnitCount} units but received " +
                    $"{this.units.Count}."
                );
            }

            Units =
                new ReadOnlyCollection<SpatialUnit>(
                    this.units
                );

            Bounds =
                CalculateBounds(
                    this.units
                );
        }


        public bool TryGetUnit(
            string unitId,
            out SpatialUnit unit
        )
        {
            if (string.IsNullOrWhiteSpace(
                    unitId
                ))
            {
                unit = null;
                return false;
            }

            return unitsById.TryGetValue(
                unitId.Trim(),
                out unit
            );
        }


        public SpatialUnit GetUnit(
            string unitId
        )
        {
            if (!TryGetUnit(
                    unitId,
                    out SpatialUnit unit
                ))
            {
                throw new KeyNotFoundException(
                    $"Spatial unit '{unitId}' was not found " +
                    $"in spatial layer '{Id}'."
                );
            }

            return unit;
        }


        public bool ContainsUnit(
            string unitId
        )
        {
            return TryGetUnit(
                unitId,
                out _
            );
        }


        private static SpatialBounds CalculateBounds(
            IReadOnlyList<SpatialUnit> units
        )
        {
            SpatialBounds first =
                units[0].Bounds;

            double minEasting =
                first.MinEasting;

            double minNorthing =
                first.MinNorthing;

            double minElevation =
                first.MinElevation;

            double maxEasting =
                first.MaxEasting;

            double maxNorthing =
                first.MaxNorthing;

            double maxElevation =
                first.MaxElevation;


            for (
                int i = 1;
                i < units.Count;
                i++
            )
            {
                SpatialBounds bounds =
                    units[i].Bounds;

                minEasting =
                    Math.Min(
                        minEasting,
                        bounds.MinEasting
                    );

                minNorthing =
                    Math.Min(
                        minNorthing,
                        bounds.MinNorthing
                    );

                minElevation =
                    Math.Min(
                        minElevation,
                        bounds.MinElevation
                    );

                maxEasting =
                    Math.Max(
                        maxEasting,
                        bounds.MaxEasting
                    );

                maxNorthing =
                    Math.Max(
                        maxNorthing,
                        bounds.MaxNorthing
                    );

                maxElevation =
                    Math.Max(
                        maxElevation,
                        bounds.MaxElevation
                    );
            }


            return new SpatialBounds(
                minEasting,
                minNorthing,
                minElevation,

                maxEasting,
                maxNorthing,
                maxElevation
            );
        }


        public override string ToString()
        {
            return
                $"{DisplayName} [{Id}] - " +
                $"{UnitCount} units, " +
                $"{Definition.GeometryType}";
        }
    }
}
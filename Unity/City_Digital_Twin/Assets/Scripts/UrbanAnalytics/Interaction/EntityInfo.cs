using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

using UrbanAnalytics.Data;
using UrbanAnalytics.Spatial;
using UrbanAnalytics.Spatial.Geometry;
using UrbanAnalytics.UrbanContext;
using UrbanAnalytics.Visualization;

namespace UrbanAnalytics.Interaction
{
    /// <summary>
    /// One labelled value about an entity.
    /// </summary>
    public sealed class EntityInfoRow
    {
        public string Label;

        public string ValueText;

        public string Unit;

        /// <summary>
        /// Numeric value, or null for text / missing values.
        /// </summary>
        public double? Value;

        /// <summary>
        /// Percentile rank (0–100) of Value among all units with a
        /// valid value of the same variable (mid-rank for ties).
        /// </summary>
        public double? Percentile;

        /// <summary>
        /// "dataLayerId/variableId" for data values; null for
        /// descriptive rows. Rows with the same key are compared.
        /// </summary>
        public string Key;

        /// <summary>
        /// True when the active visualization encodes this
        /// variable.
        /// </summary>
        public bool IsEncoded;

        /// <summary>
        /// Short context shown with highlighted values, e.g. the
        /// channel ("Height ↑") and, for a building, the area the
        /// value belongs to ("Furutorp").
        /// </summary>
        public string Note;
    }


    public sealed class EntityInfoSection
    {
        public string Title;

        public string Subtitle;

        /// <summary>Detail section: shown folded until opened.</summary>
        public bool Collapsed;

        public readonly List<EntityInfoRow> Rows =
            new List<EntityInfoRow>();
    }


    /// <summary>
    /// Everything the runtime knows about one entity, ready for
    /// display: identity, geometry facts and every variable of
    /// every data layer on its spatial unit.
    /// </summary>
    public sealed class EntityInfo
    {
        public EntityReference Entity;

        public string Title;

        public string Subtitle;

        /// <summary>
        /// The values the active view shows for this entity, in
        /// layer order (for a building: the values of the units it
        /// belongs to). Shown first, large.
        /// </summary>
        public readonly List<EntityInfoRow> Highlights =
            new List<EntityInfoRow>();

        public readonly List<EntityInfoSection> Sections =
            new List<EntityInfoSection>();

        /// <summary>Technical identifiers, shown small at the bottom.</summary>
        public string Footer;


        public IEnumerable<EntityInfoRow> DataRows
        {
            get
            {
                foreach (EntityInfoSection section in Sections)
                {
                    foreach (EntityInfoRow row in section.Rows)
                    {
                        if (row.Key != null)
                        {
                            yield return row;
                        }
                    }
                }
            }
        }


        public bool TryGetDataRow(
            string key,
            out EntityInfoRow row
        )
        {
            foreach (EntityInfoRow candidate in DataRows)
            {
                if (string.Equals(
                        candidate.Key,
                        key,
                        StringComparison.Ordinal
                    ))
                {
                    row =
                        candidate;

                    return true;
                }
            }


            row =
                null;

            return false;
        }
    }


    /// <summary>
    /// Builds EntityInfo from the loaded data. Values come
    /// straight from the data layers (no scale or encoding is
    /// applied), so they can be checked against the source files.
    /// </summary>
    public sealed class EntityInfoBuilder
    {
        private readonly InteractionContext context;


        // Sorted valid values per "layer/variable", for
        // percentile ranks. Data layers are static at runtime.
        private readonly Dictionary<string, double[]>
            sortedValues =
                new Dictionary<string, double[]>(
                    StringComparer.Ordinal
                );


        public EntityInfoBuilder(
            InteractionContext context
        )
        {
            this.context =
                context
                ?? throw new ArgumentNullException(
                    nameof(context)
                );
        }


        public EntityInfo Build(
            EntityReference entity,
            VisualizationSpec activeVisualization
        )
        {
            var info =
                new EntityInfo
                {
                    Entity =
                        entity
                };


            if (!entity.IsValid)
            {
                return info;
            }


            HashSet<string> encodedKeys =
                CollectEncodedKeys(
                    activeVisualization
                );


            if (entity.Kind == EntityKind.Building)
            {
                AddBuildingSection(
                    info,
                    entity
                );
            }


            if (entity.HasUnit)
            {
                AddUnitSections(
                    info,
                    entity,
                    encodedKeys
                );
            }


            AddHighlights(
                info,
                entity,
                activeVisualization
            );


            if (entity.Kind == EntityKind.SpatialUnit)
            {
                string unitName =
                    UnitDisplayName(
                        entity.SpatialLayerId,
                        entity.UnitId
                    );

                string areaName =
                    AreaName(
                        entity.SpatialLayerId,
                        entity.UnitId
                    );

                info.Title =
                    unitName ?? LayerDisplayName(entity.SpatialLayerId);

                info.Subtitle =
                    areaName != null && areaName != unitName
                        ? $"in {areaName} · {LayerDisplayName(entity.SpatialLayerId)}"
                        : LayerDisplayName(entity.SpatialLayerId);

                info.Footer =
                    $"id {EntityReference.ShortId(entity.Id)}";
            }
            else if (TryGetBuildingAttributes(
                         entity.Id,
                         out BuildingAttributeTable attributes
                     ))
            {
                string name =
                    FirstValue(attributes, entity.Id, "name");

                string type =
                    FirstValue(attributes, entity.Id, "purpose", "type");

                string area =
                    BuildingPlaceName(entity) ??
                    (entity.HasUnit
                        ? AreaName(entity.SpatialLayerId, entity.UnitId)
                        : null);

                info.Title =
                    name ?? type ?? "Building";

                info.Subtitle =
                    JoinNonEmpty(
                        name != null ? type : null,
                        area != null ? "in " + area : null
                    );

                info.Footer =
                    $"building {EntityReference.ShortId(entity.Id)}";
            }
            else
            {
                info.Title =
                    "Building";

                info.Subtitle =
                    entity.HasUnit
                        ? $"in {AreaName(entity.SpatialLayerId, entity.UnitId) ?? LayerDisplayName(entity.SpatialLayerId)}"
                        : string.Empty;

                info.Footer =
                    $"building {EntityReference.ShortId(entity.Id)}";
            }


            return info;
        }


        // =========================================================
        // HIGHLIGHTS ("in this view")
        // =========================================================

        private Associations.AssociationManager associations;


        /// <summary>
        /// One row per variable the active view encodes, with the
        /// value for this entity: the entity's own unit, or for a
        /// building the unit it belongs to in that variable's
        /// spatial layer (association "buildings_to_&lt;layer&gt;").
        /// A building also gets its height first.
        /// </summary>
        private void AddHighlights(
            EntityInfo info,
            EntityReference entity,
            VisualizationSpec visualization
        )
        {
            if (entity.Kind == EntityKind.Building &&
                TryGetBuildingAttributes(entity.Id, out BuildingAttributeTable attributes) &&
                attributes.TryGetValue(entity.Id, "height", out BuildingAttributeValue height))
            {
                info.Highlights.Add(
                    new EntityInfoRow
                    {
                        Label = "Building height",
                        ValueText = height.Text,
                        Unit = "m",
                        Value = height.Number
                    }
                );
            }

            if (visualization == null)
            {
                return;
            }

            var seen =
                new HashSet<string>(StringComparer.Ordinal);

            foreach ((DataVariableReference variable, string channel) in EncodedVariables(visualization))
            {
                string key =
                    Key(variable.DataLayerId, variable.VariableId);

                if (!seen.Add(key) ||
                    !context.DataLayers.TryGetLayer(variable.DataLayerId, out DataLayer dataLayer) ||
                    !dataLayer.TryGetVariableDefinition(variable.VariableId, out DataVariableDefinition definition))
                {
                    continue;
                }

                string layerId =
                    dataLayer.TargetSpatialLayerId;

                string unitId =
                    UnitIn(entity, layerId);

                if (unitId == null)
                {
                    continue;
                }

                var row =
                    new EntityInfoRow
                    {
                        Label = string.IsNullOrWhiteSpace(definition.DisplayName) ? variable.VariableId : definition.DisplayName,
                        Unit = definition.Unit,
                        Key = key,
                        IsEncoded = true,
                        ValueText = "no data",
                        Note = entity.Kind == EntityKind.Building
                            ? JoinNonEmpty(channel, UnitDisplayName(layerId, unitId))
                            : channel
                    };

                if (dataLayer.TryGetDouble(unitId, variable.VariableId, out double value))
                {
                    row.Value = value;
                    row.ValueText = FormatNumber(value);
                    row.Percentile = PercentileRank(dataLayer, variable.VariableId, value);
                }
                else if (dataLayer.TryGetString(unitId, variable.VariableId, out string text) && text != null)
                {
                    row.ValueText = text;
                }

                info.Highlights.Add(
                    row
                );
            }
        }


        /// <summary>The entity's unit in a spatial layer, or null.</summary>
        private string UnitIn(
            EntityReference entity,
            string layerId
        )
        {
            string prefix =
                layerId + ":";

            if (entity.HasUnit &&
                entity.UnitId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return entity.UnitId;
            }

            if (entity.Kind != EntityKind.Building)
            {
                return null;
            }

            if (associations == null)
            {
                associations =
                    UnityEngine.Object.FindFirstObjectByType<Associations.AssociationManager>();
            }

            return associations != null &&
                   associations.TryResolve("buildings_to_" + layerId, entity.Id, out string unitId)
                ? unitId
                : null;
        }


        /// <summary>Every encoded variable of a view with a channel label, in layer order.</summary>
        private static IEnumerable<(DataVariableReference, string)> EncodedVariables(
            VisualizationSpec visualization
        )
        {
            foreach (VisualizationLayerSpec layer in visualization.Layers)
            {
                if (layer == null || !layer.Enabled)
                {
                    continue;
                }

                bool buildings =
                    layer.Target != null &&
                    layer.Target.Kind == VisualizationTargetKind.UrbanContextLayer;

                foreach (VisualizationEncodingSpec encoding in layer.Encodings)
                {
                    if (encoding?.Data?.Variables == null)
                    {
                        continue;
                    }

                    string channel =
                        ChannelLabel(encoding, buildings, layer.Mark);

                    foreach (DataVariableReference variable in encoding.Data.Variables)
                    {
                        if (variable != null && variable.IsConfigured)
                        {
                            yield return (variable, channel);
                        }
                    }
                }
            }
        }


        private static string ChannelLabel(
            VisualizationEncodingSpec encoding,
            bool buildings,
            VisualizationMark mark
        )
        {
            switch (encoding.Channel)
            {
                case VisualizationChannel.Color:
                    return buildings ? "building colour" : "colour";

                case VisualizationChannel.Height:
                    return encoding.Role == VisualizationEncodingRole.Positive ? "height ↑"
                        : encoding.Role == VisualizationEncodingRole.Negative ? "height ↓"
                        : "height";

                case VisualizationChannel.Segments:
                    return mark == VisualizationMark.RadialGlyph ? "glyph" : "bar segment";

                default:
                    return encoding.Channel.ToString().ToLowerInvariant();
            }
        }


        /// <summary>Display name of a unit (e.g. a district name), or null.</summary>
        private string UnitDisplayName(
            string layerId,
            string unitId
        )
        {
            return context.SpatialLayers.TryGetUnit(layerId, unitId, out SpatialUnit unit) &&
                   !string.IsNullOrWhiteSpace(unit.DisplayName) &&
                   unit.DisplayName != unitId &&
                   unit.DisplayName != EntityReference.ShortId(unitId)
                ? unit.DisplayName
                : null;
        }


        /// <summary>
        /// A place name for a unit: the String variable "area_name"
        /// of any data layer on its spatial layer (e.g. the voting
        /// district a grid cell lies in), or the unit's own name for
        /// named units such as voting districts.
        /// </summary>
        private string AreaName(
            string layerId,
            string unitId
        )
        {
            foreach (KeyValuePair<string, DataLayer> pair in context.DataLayers.LoadedLayers)
            {
                DataLayer layer = pair.Value;

                if (string.Equals(layer.TargetSpatialLayerId, layerId, StringComparison.Ordinal) &&
                    layer.ContainsVariable("area_name") &&
                    layer.TryGetString(unitId, "area_name", out string name) &&
                    !string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            string own =
                UnitDisplayName(layerId, unitId);

            return own != null && !IsGenericName(own)
                ? own
                : null;
        }


        /// <summary>
        /// The name of a named unit the building belongs to (e.g. its
        /// voting district), through the "buildings_to_*" links.
        /// Generic units (grid cells, DeSO areas) are skipped.
        /// </summary>
        private string BuildingPlaceName(
            EntityReference building
        )
        {
            if (associations == null)
            {
                associations =
                    UnityEngine.Object.FindFirstObjectByType<Associations.AssociationManager>();
            }

            if (associations == null)
            {
                return null;
            }

            foreach (string associationId in associations.AssociationIds)
            {
                if (!associationId.StartsWith("buildings_to_", StringComparison.Ordinal) ||
                    !associations.TryResolve(associationId, building.Id, out string unitId))
                {
                    continue;
                }

                string layerId =
                    associationId.Substring("buildings_to_".Length);

                string name =
                    UnitDisplayName(layerId, unitId);

                if (name != null && !IsGenericName(name))
                {
                    return name;
                }
            }

            return null;
        }


        private static bool IsGenericName(
            string name
        )
        {
            return name.EndsWith("grid cell", StringComparison.Ordinal) ||
                   name == "DeSO area";
        }


        private static string JoinNonEmpty(
            params string[] parts
        )
        {
            var kept =
                new List<string>();

            foreach (string part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    kept.Add(part);
                }
            }

            return string.Join(" · ", kept);
        }


        // =========================================================
        // SECTIONS
        // =========================================================

        private void AddBuildingSection(
            EntityInfo info,
            EntityReference entity
        )
        {
            if (TryGetBuildingAttributes(
                    entity.Id,
                    out BuildingAttributeTable attributes
                ))
            {
                AddBuildingAttributeSections(
                    info,
                    entity,
                    attributes
                );

                return;
            }


            var section =
                new EntityInfoSection
                {
                    Title =
                        "Building"
                };


            section.Rows.Add(
                Text(
                    "ID",
                    entity.Id
                )
            );


            BuildingIndex index =
                context.Buildings;


            if (index != null &&
                index.TryGet(
                    entity.Id,
                    out BuildingIndex.Location location
                ) &&
                !float.IsNaN(
                    location.Range.HeightMeters
                ))
            {
                section.Rows.Add(
                    Text(
                        "Height",
                        FormatNumber(
                            location.Range.HeightMeters
                        ),
                        "m"
                    )
                );
            }


            section.Rows.Add(
                Text(
                    "Cell",
                    entity.HasUnit
                        ? entity.UnitId
                        : "none (outside every cell)"
                )
            );


            info.Sections.Add(
                section
            );
        }


        /// <summary>
        /// Building package attributes: one section per field
        /// group, in the order of the field list in layer.json.
        /// Fields without a value for this building are left out.
        /// </summary>
        private static void AddBuildingAttributeSections(
            EntityInfo info,
            EntityReference entity,
            BuildingAttributeTable attributes
        )
        {
            var sections =
                new Dictionary<string, EntityInfoSection>(
                    StringComparer.Ordinal
                );


            foreach (BuildingAttributeValue value in attributes.GetValues(entity.Id))
            {
                string group =
                    string.IsNullOrWhiteSpace(
                        value.Field.group
                    )
                        ? "Building"
                        : value.Field.group;


                if (!sections.TryGetValue(
                        group,
                        out EntityInfoSection section
                    ))
                {
                    section =
                        new EntityInfoSection
                        {
                            Title =
                                group,

                            Collapsed =
                                true
                        };

                    sections.Add(
                        group,
                        section
                    );

                    info.Sections.Add(
                        section
                    );
                }


                section.Rows.Add(
                    new EntityInfoRow
                    {
                        Label =
                            value.Field.displayName,

                        ValueText =
                            value.Text,

                        Unit =
                            string.IsNullOrWhiteSpace(
                                value.Field.unit
                            )
                                ? null
                                : value.Field.unit,

                        Value =
                            value.Number
                    }
                );
            }


        }


        private bool TryGetBuildingAttributes(
            string buildingId,
            out BuildingAttributeTable attributes
        )
        {
            attributes =
                context.UrbanContext != null
                    ? context.UrbanContext.BuildingAttributes
                    : null;

            return attributes != null &&
                   attributes.Contains(
                       buildingId
                   );
        }


        private static string FirstValue(
            BuildingAttributeTable attributes,
            string buildingId,
            params string[] fieldIds
        )
        {
            foreach (string fieldId in fieldIds)
            {
                if (attributes.TryGetValue(
                        buildingId,
                        fieldId,
                        out BuildingAttributeValue value
                    ))
                {
                    return value.Text;
                }
            }


            return null;
        }


        private void AddUnitSections(
            EntityInfo info,
            EntityReference entity,
            HashSet<string> encodedKeys
        )
        {
            string layerId =
                entity.SpatialLayerId;

            string unitId =
                entity.UnitId;


            // ---------------------------------------------------
            // Cell facts
            // ---------------------------------------------------

            var cell =
                new EntityInfoSection
                {
                    Title =
                        "About this area",

                    Collapsed =
                        true
                };


            if (context.SpatialLayers.TryGetUnit(
                    layerId,
                    unitId,
                    out SpatialUnit unit
                ))
            {
                double area =
                    AreaOf(
                        unit.Geometry
                    );


                if (area > 0.0)
                {
                    cell.Rows.Add(
                        Number(
                            "Area",
                            Math.Round(area / 1_000_000.0, 3),
                            "km²",
                            "cell/area"
                        )
                    );
                }
            }


            BuildingIndex buildings =
                context.Buildings;


            if (buildings != null)
            {
                AddBuildingStatistics(
                    cell,
                    buildings.GetForUnit(
                        unitId
                    )
                );
            }


            info.Sections.Add(
                cell
            );


            // ---------------------------------------------------
            // Every data layer on this spatial layer
            // ---------------------------------------------------

            var dataLayers =
                new List<DataLayer>();


            foreach (
                KeyValuePair<string, DataLayer> pair
                in context.DataLayers.LoadedLayers
            )
            {
                // Place-name layers only label the unit (title/subtitle).
                if (string.Equals(
                        pair.Value.TargetSpatialLayerId,
                        layerId,
                        StringComparison.Ordinal
                    ) &&
                    !(pair.Value.Definition.Variables.Count == 1 &&
                      pair.Value.ContainsVariable("area_name")))
                {
                    dataLayers.Add(
                        pair.Value
                    );
                }
            }


            dataLayers.Sort(
                (a, b) =>
                    string.CompareOrdinal(
                        a.Id,
                        b.Id
                    )
            );


            foreach (DataLayer dataLayer in dataLayers)
            {
                info.Sections.Add(
                    BuildDataSection(
                        dataLayer,
                        unitId,
                        encodedKeys
                    )
                );
            }
        }


        /// <summary>
        /// Count, mean and maximum height of the buildings
        /// associated with the cell (source heights, metres).
        /// </summary>
        private static void AddBuildingStatistics(
            EntityInfoSection section,
            IReadOnlyList<BuildingIndex.Location> buildings
        )
        {
            section.Rows.Add(
                Number(
                    "Buildings",
                    buildings.Count,
                    null,
                    "cell/buildings"
                )
            );


            double sum =
                0.0;

            double maximum =
                double.NegativeInfinity;

            int withHeight =
                0;


            foreach (BuildingIndex.Location location in buildings)
            {
                float height =
                    location.Range.HeightMeters;


                if (float.IsNaN(height))
                {
                    continue;
                }


                sum +=
                    height;

                maximum =
                    Math.Max(
                        maximum,
                        height
                    );

                withHeight++;
            }


            if (withHeight == 0)
            {
                return;
            }


            section.Rows.Add(
                Number(
                    "Mean building height",
                    sum / withHeight,
                    "m",
                    "cell/building_height_mean"
                )
            );


            section.Rows.Add(
                Number(
                    "Tallest building",
                    maximum,
                    "m",
                    "cell/building_height_max"
                )
            );
        }


        private EntityInfoSection BuildDataSection(
            DataLayer dataLayer,
            string unitId,
            HashSet<string> encodedKeys
        )
        {
            var section =
                new EntityInfoSection
                {
                    Title =
                        string.IsNullOrWhiteSpace(
                            dataLayer.DisplayName
                        )
                            ? dataLayer.Id
                            : dataLayer.DisplayName,

                    Collapsed =
                        true
                };


            bool hasUnit =
                dataLayer.ContainsUnit(
                    unitId
                );


            if (!hasUnit)
            {
                section.Rows.Add(
                    Text(
                        "No data for this area",
                        string.Empty
                    )
                );

                return section;
            }


            foreach (
                DataVariableDefinition variable
                in dataLayer.Definition.Variables
            )
            {
                string key =
                    Key(
                        dataLayer.Id,
                        variable.Id
                    );


                var row =
                    new EntityInfoRow
                    {
                        Label =
                            string.IsNullOrWhiteSpace(
                                variable.DisplayName
                            )
                                ? variable.Id
                                : variable.DisplayName,

                        Unit =
                            variable.Unit,

                        Key =
                            key,

                        IsEncoded =
                            encodedKeys.Contains(
                                key
                            ),

                        ValueText =
                            "no data"
                    };


                if (!hasUnit)
                {
                    section.Rows.Add(
                        row
                    );

                    continue;
                }


                if (dataLayer.TryGetDouble(
                        unitId,
                        variable.Id,
                        out double value
                    ))
                {
                    row.Value =
                        value;

                    row.ValueText =
                        FormatNumber(
                            value
                        );

                    row.Percentile =
                        PercentileRank(
                            dataLayer,
                            variable.Id,
                            value
                        );
                }
                else if (dataLayer.TryGetString(
                             unitId,
                             variable.Id,
                             out string text
                         ) &&
                         text != null)
                {
                    row.ValueText =
                        text;
                }
                else if (dataLayer.TryGetBoolean(
                             unitId,
                             variable.Id,
                             out bool flag
                         ))
                {
                    row.ValueText =
                        flag
                            ? "yes"
                            : "no";
                }


                section.Rows.Add(
                    row
                );
            }


            return section;
        }


        // =========================================================
        // ENCODED VARIABLES
        // =========================================================

        public static HashSet<string> CollectEncodedKeys(
            VisualizationSpec visualization
        )
        {
            var keys =
                new HashSet<string>(
                    StringComparer.Ordinal
                );


            if (visualization == null)
            {
                return keys;
            }


            foreach (
                VisualizationLayerSpec layer
                in visualization.Layers
            )
            {
                if (layer == null ||
                    !layer.Enabled ||
                    layer.Encodings == null)
                {
                    continue;
                }


                foreach (
                    VisualizationEncodingSpec encoding
                    in layer.Encodings
                )
                {
                    if (encoding?.Data?.Variables == null)
                    {
                        continue;
                    }


                    foreach (
                        DataVariableReference variable
                        in encoding.Data.Variables
                    )
                    {
                        if (variable != null &&
                            variable.IsConfigured)
                        {
                            keys.Add(
                                Key(
                                    variable.DataLayerId,
                                    variable.VariableId
                                )
                            );
                        }
                    }
                }
            }


            return keys;
        }


        // =========================================================
        // PERCENTILE
        // =========================================================

        /// <summary>
        /// Share (0–100) of valid values below value, counting
        /// equal values as half (mid-rank).
        /// </summary>
        public double? PercentileRank(
            DataLayer dataLayer,
            string variableId,
            double value
        )
        {
            string key =
                Key(
                    dataLayer.Id,
                    variableId
                );


            if (!sortedValues.TryGetValue(
                    key,
                    out double[] sorted
                ))
            {
                if (!dataLayer.TryGetNumericValues(
                        variableId,
                        out sorted
                    ) ||
                    sorted == null)
                {
                    sorted =
                        Array.Empty<double>();
                }


                Array.Sort(
                    sorted
                );


                sortedValues[key] =
                    sorted;
            }


            return PercentileRank(
                sorted,
                value
            );
        }


        public static double? PercentileRank(
            double[] sorted,
            double value
        )
        {
            if (sorted == null ||
                sorted.Length == 0)
            {
                return null;
            }


            int below =
                LowerBound(
                    sorted,
                    value
                );

            int notAbove =
                UpperBound(
                    sorted,
                    value
                );


            double rank =
                below +
                0.5 * (notAbove - below);


            return
                100.0 *
                rank /
                sorted.Length;
        }


        private static int LowerBound(
            double[] sorted,
            double value
        )
        {
            int low =
                0;

            int high =
                sorted.Length;


            while (low < high)
            {
                int middle =
                    (low + high) / 2;


                if (sorted[middle] < value)
                {
                    low =
                        middle + 1;
                }
                else
                {
                    high =
                        middle;
                }
            }


            return low;
        }


        private static int UpperBound(
            double[] sorted,
            double value
        )
        {
            int low =
                0;

            int high =
                sorted.Length;


            while (low < high)
            {
                int middle =
                    (low + high) / 2;


                if (sorted[middle] <= value)
                {
                    low =
                        middle + 1;
                }
                else
                {
                    high =
                        middle;
                }
            }


            return low;
        }


        // =========================================================
        // FORMATTING
        // =========================================================

        public static string Key(
            string dataLayerId,
            string variableId
        )
        {
            return
                dataLayerId +
                "/" +
                variableId;
        }


        /// <summary>
        /// Thousands separators for large values, up to two
        /// decimals for small ones. Invariant culture.
        /// </summary>
        public static string FormatNumber(
            double value
        )
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return "—";
            }


            double magnitude =
                Math.Abs(
                    value
                );


            if (magnitude >= 1000.0 ||
                Math.Abs(value - Math.Round(value)) < 1e-9)
            {
                return value.ToString(
                    "#,0",
                    CultureInfo.InvariantCulture
                );
            }


            return value.ToString(
                magnitude >= 10.0
                    ? "#,0.#"
                    : "0.##",
                CultureInfo.InvariantCulture
            );
        }


        public static string FormatPercentile(
            double? percentile
        )
        {
            return percentile.HasValue
                ? "p" +
                  Math.Round(percentile.Value).ToString(
                      "0",
                      CultureInfo.InvariantCulture
                  )
                : string.Empty;
        }


        private string LayerDisplayName(
            string spatialLayerId
        )
        {
            return
                context.SpatialLayers.TryGetLayer(
                    spatialLayerId,
                    out SpatialLayer layer
                ) &&
                !string.IsNullOrWhiteSpace(
                    layer.DisplayName
                )
                    ? layer.DisplayName
                    : spatialLayerId;
        }


        private static double AreaOf(
            ISpatialGeometry geometry
        )
        {
            switch (geometry)
            {
                case PolygonGeometry polygon:
                    return polygon.Area;

                case MultiPolygonGeometry multiPolygon:
                    return multiPolygon.Area;

                default:
                    return 0.0;
            }
        }


        /// <summary>
        /// A derived numeric fact (not from a data layer) that
        /// can still be compared between blocks.
        /// </summary>
        private static EntityInfoRow Number(
            string label,
            double value,
            string unit,
            string key
        )
        {
            return new EntityInfoRow
            {
                Label =
                    label,

                Value =
                    value,

                ValueText =
                    FormatNumber(
                        value
                    ),

                Unit =
                    unit,

                Key =
                    key
            };
        }


        private static EntityInfoRow Text(
            string label,
            string value,
            string unit = null
        )
        {
            return new EntityInfoRow
            {
                Label =
                    label,

                ValueText =
                    value,

                Unit =
                    unit
            };
        }
    }
}

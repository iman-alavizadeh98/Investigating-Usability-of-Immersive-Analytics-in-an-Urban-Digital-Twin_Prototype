using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using UnityEngine;

using UrbanAnalytics.Data.Serialization;
using UrbanAnalytics.Spatial.Geometry;

namespace UrbanAnalytics.UrbanContext
{
    // =============================================================
    // layer.json
    // =============================================================

    /// <summary>
    /// urban_context/buildings/layer.json, written by
    /// Src/pipelines/unity_package/buildings.py. Only the fields
    /// the runtime needs are declared; JsonUtility ignores the rest
    /// (provenance, checksums, statistics).
    /// </summary>
    [Serializable]
    public sealed class BuildingContextDefinition
    {
        public string schemaVersion;

        public string id;

        public string kind;

        public string displayName;

        public string description;

        public string geometryFile;

        public string geometryFormat;

        public string attributesFile;

        public int buildingCount;

        public int buildingsWithHeight;

        public string idPrefix;

        public string sourceCRS;

        public BuildingFieldDefinition[] fields;
    }


    /// <summary>
    /// One attribute shown in the building info panel. The list
    /// order is the display order; groups appear in order of
    /// first use.
    /// </summary>
    [Serializable]
    public sealed class BuildingFieldDefinition
    {
        public string id;

        public string column;

        public string displayName;

        public string group;

        public string valueType;

        public string unit;

        public string description;
    }


    // =============================================================
    // geometry.bin (GBLD v3)
    // =============================================================

    public sealed class BuildingRecord
    {
        public string Id;

        public float HeightMeters;

        /// <summary>Metres above sea level; NaN when unknown.</summary>
        public float GroundZ;

        public bool HasHeight;

        public readonly List<PolygonGeometry> Polygons =
            new List<PolygonGeometry>();
    }


    /// <summary>
    /// Reader for GBLD version 3. Format (little-endian):
    ///
    ///   char[4] "GBLD", uint32 version = 3, uint32 count,
    ///   double originEasting, double originNorthing
    ///   per building:
    ///     uint16 idLength, byte[idLength] id (UTF-8)
    ///     float32 height, float32 groundZ (NaN = unknown),
    ///     uint8 flags (bit 0 = has height), uint16 polygonCount
    ///     per polygon: uint16 ringCount (exterior, then holes)
    ///       per ring: uint32 vertexCount,
    ///                 vertexCount x (float32 x, float32 z)
    ///
    /// x / z are metres relative to the origin in the header
    /// (easting / northing). Rings are not closed.
    /// </summary>
    public static class BuildingGeometryReader
    {
        public const uint Version = 3;

        private const uint MaxVertices = 1000000;


        public static bool IsVersion3(
            byte[] bytes
        )
        {
            return bytes != null &&
                   bytes.Length >= 8 &&
                   bytes[0] == (byte)'G' &&
                   bytes[1] == (byte)'B' &&
                   bytes[2] == (byte)'L' &&
                   bytes[3] == (byte)'D' &&
                   BitConverter.ToUInt32(bytes, 4) == Version;
        }


        public static List<BuildingRecord> Read(
            byte[] bytes,
            out double originEasting,
            out double originNorthing
        )
        {
            using var stream =
                new MemoryStream(
                    bytes,
                    false
                );

            using var reader =
                new BinaryReader(
                    stream,
                    Encoding.UTF8,
                    false
                );


            string magic =
                Encoding.ASCII.GetString(
                    reader.ReadBytes(4)
                );

            if (magic != "GBLD")
            {
                throw new InvalidDataException(
                    $"Invalid building geometry magic '{magic}'."
                );
            }


            uint version =
                reader.ReadUInt32();

            if (version != Version)
            {
                throw new InvalidDataException(
                    $"Building geometry version {version}; " +
                    $"expected {Version}."
                );
            }


            uint count =
                reader.ReadUInt32();

            originEasting =
                reader.ReadDouble();

            originNorthing =
                reader.ReadDouble();


            var records =
                new List<BuildingRecord>(
                    (int)count
                );


            for (uint i = 0; i < count; i++)
            {
                ushort idLength =
                    reader.ReadUInt16();

                var record =
                    new BuildingRecord
                    {
                        Id =
                            Encoding.UTF8.GetString(
                                reader.ReadBytes(idLength)
                            ),

                        HeightMeters =
                            reader.ReadSingle(),

                        GroundZ =
                            reader.ReadSingle()
                    };


                byte flags =
                    reader.ReadByte();

                record.HasHeight =
                    (flags & 1) != 0;


                ushort polygonCount =
                    reader.ReadUInt16();


                for (int p = 0; p < polygonCount; p++)
                {
                    ushort ringCount =
                        reader.ReadUInt16();


                    PolygonRing exterior =
                        null;

                    var holes =
                        new List<PolygonRing>();


                    for (int r = 0; r < ringCount; r++)
                    {
                        PolygonRing ring =
                            ReadRing(
                                reader,
                                record.Id,
                                originEasting,
                                originNorthing
                            );


                        if (r == 0)
                        {
                            exterior =
                                ring;
                        }
                        else
                        {
                            holes.Add(
                                ring
                            );
                        }
                    }


                    if (exterior != null)
                    {
                        record.Polygons.Add(
                            new PolygonGeometry(
                                exterior,
                                holes
                            )
                        );
                    }
                }


                records.Add(
                    record
                );
            }


            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException(
                    $"{stream.Length - stream.Position} trailing " +
                    $"bytes in building geometry."
                );
            }


            return records;
        }


        private static PolygonRing ReadRing(
            BinaryReader reader,
            string buildingId,
            double originEasting,
            double originNorthing
        )
        {
            uint vertexCount =
                reader.ReadUInt32();


            if (vertexCount > MaxVertices)
            {
                throw new InvalidDataException(
                    $"Building '{buildingId}' has an implausible " +
                    $"ring of {vertexCount} vertices."
                );
            }


            var coordinates =
                new SpatialCoordinate[vertexCount];


            for (int v = 0; v < vertexCount; v++)
            {
                float x =
                    reader.ReadSingle();

                float z =
                    reader.ReadSingle();

                coordinates[v] =
                    new SpatialCoordinate(
                        originEasting + x,
                        originNorthing + z
                    );
            }


            return new PolygonRing(
                coordinates
            );
        }
    }


    // =============================================================
    // attributes.json
    // =============================================================

    /// <summary>
    /// One formatted attribute value for display.
    /// </summary>
    public readonly struct BuildingAttributeValue
    {
        public BuildingFieldDefinition Field
        {
            get;
        }

        public string Text
        {
            get;
        }

        /// <summary>Numeric value for Float / Integer fields.</summary>
        public double? Number
        {
            get;
        }


        public BuildingAttributeValue(
            BuildingFieldDefinition field,
            string text,
            double? number
        )
        {
            Field =
                field;

            Text =
                text;

            Number =
                number;
        }
    }


    /// <summary>
    /// Per-building attributes from attributes.json (same layout
    /// as a data-layer values file), described by the field list
    /// in layer.json. Missing values (valid = false) are skipped.
    /// </summary>
    public sealed class BuildingAttributeTable
    {
        private readonly BuildingFieldDefinition[] fields;

        private readonly DataColumnDto[] columns;

        private readonly Dictionary<string, int> rowById;


        public IReadOnlyList<BuildingFieldDefinition> Fields =>
            fields;

        public int Count =>
            rowById.Count;


        private BuildingAttributeTable(
            BuildingFieldDefinition[] fields,
            DataColumnDto[] columns,
            Dictionary<string, int> rowById
        )
        {
            this.fields =
                fields;

            this.columns =
                columns;

            this.rowById =
                rowById;
        }


        public static BuildingAttributeTable Parse(
            string json,
            BuildingFieldDefinition[] fieldDefinitions
        )
        {
            DataLayerFileDto file =
                JsonUtility.FromJson<DataLayerFileDto>(
                    json
                );


            if (file?.unitIds == null)
            {
                throw new InvalidDataException(
                    "Building attributes contain no unitIds."
                );
            }


            int rowCount =
                file.unitIds.Length;


            var byVariable =
                new Dictionary<string, DataColumnDto>(
                    StringComparer.Ordinal
                );

            foreach (DataColumnDto column in file.columns ?? Array.Empty<DataColumnDto>())
            {
                if (column?.variableId != null)
                {
                    byVariable[column.variableId] =
                        column;
                }
            }


            var usedFields =
                new List<BuildingFieldDefinition>();

            var usedColumns =
                new List<DataColumnDto>();


            foreach (BuildingFieldDefinition field in fieldDefinitions ?? Array.Empty<BuildingFieldDefinition>())
            {
                if (field?.id == null ||
                    !byVariable.TryGetValue(
                        field.id,
                        out DataColumnDto column
                    ))
                {
                    Debug.LogWarning(
                        $"Building field '{field?.id}' has no " +
                        $"column in attributes.json; skipped."
                    );

                    continue;
                }


                int length =
                    ValueCount(
                        field,
                        column
                    );

                if (length != rowCount ||
                    (column.valid != null &&
                     column.valid.Length != 0 &&
                     column.valid.Length != rowCount))
                {
                    throw new InvalidDataException(
                        $"Building field '{field.id}' has " +
                        $"{length} values for {rowCount} buildings."
                    );
                }


                usedFields.Add(
                    field
                );

                usedColumns.Add(
                    column
                );
            }


            var rows =
                new Dictionary<string, int>(
                    rowCount,
                    StringComparer.Ordinal
                );

            for (int i = 0; i < rowCount; i++)
            {
                rows[file.unitIds[i]] =
                    i;
            }


            return new BuildingAttributeTable(
                usedFields.ToArray(),
                usedColumns.ToArray(),
                rows
            );
        }


        public bool Contains(
            string buildingId
        )
        {
            return buildingId != null &&
                   rowById.ContainsKey(buildingId);
        }


        public bool TryGetValue(
            string buildingId,
            string fieldId,
            out BuildingAttributeValue value
        )
        {
            value =
                default;

            if (buildingId == null ||
                !rowById.TryGetValue(
                    buildingId,
                    out int row
                ))
            {
                return false;
            }


            for (int i = 0; i < fields.Length; i++)
            {
                if (string.Equals(
                        fields[i].id,
                        fieldId,
                        StringComparison.Ordinal
                    ))
                {
                    return TryFormat(
                        i,
                        row,
                        out value
                    );
                }
            }


            return false;
        }


        /// <summary>
        /// Every field with a value for this building, in display
        /// order.
        /// </summary>
        public List<BuildingAttributeValue> GetValues(
            string buildingId
        )
        {
            var values =
                new List<BuildingAttributeValue>();

            if (buildingId == null ||
                !rowById.TryGetValue(
                    buildingId,
                    out int row
                ))
            {
                return values;
            }


            for (int i = 0; i < fields.Length; i++)
            {
                if (TryFormat(
                        i,
                        row,
                        out BuildingAttributeValue value
                    ))
                {
                    values.Add(
                        value
                    );
                }
            }


            return values;
        }


        private bool TryFormat(
            int fieldIndex,
            int row,
            out BuildingAttributeValue value
        )
        {
            value =
                default;

            BuildingFieldDefinition field =
                fields[fieldIndex];

            DataColumnDto column =
                columns[fieldIndex];


            if (column.valid != null &&
                column.valid.Length != 0 &&
                !column.valid[row])
            {
                return false;
            }


            switch (field.valueType)
            {
                case "Float":
                    {
                        double number =
                            column.floatValues[row];

                        value =
                            new BuildingAttributeValue(
                                field,
                                FormatFloat(number),
                                number
                            );

                        return true;
                    }

                case "Integer":
                    {
                        long number =
                            column.integerValues[row];

                        value =
                            new BuildingAttributeValue(
                                field,
                                number.ToString(
                                    CultureInfo.InvariantCulture
                                ),
                                number
                            );

                        return true;
                    }

                case "Boolean":
                    value =
                        new BuildingAttributeValue(
                            field,
                            column.booleanValues[row]
                                ? "Yes"
                                : "No",
                            null
                        );

                    return true;

                case "String":
                    {
                        string text =
                            column.stringValues[row];

                        if (string.IsNullOrWhiteSpace(text))
                        {
                            return false;
                        }

                        value =
                            new BuildingAttributeValue(
                                field,
                                text,
                                null
                            );

                        return true;
                    }

                default:
                    return false;
            }
        }


        private static int ValueCount(
            BuildingFieldDefinition field,
            DataColumnDto column
        )
        {
            switch (field.valueType)
            {
                case "Float":
                    return column.floatValues?.Length ?? 0;

                case "Integer":
                    return column.integerValues?.Length ?? 0;

                case "Boolean":
                    return column.booleanValues?.Length ?? 0;

                case "String":
                    return column.stringValues?.Length ?? 0;

                default:
                    throw new InvalidDataException(
                        $"Building field '{field.id}' has unknown " +
                        $"valueType '{field.valueType}'."
                    );
            }
        }


        private static string FormatFloat(
            double value
        )
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return "—";
            }

            return value.ToString(
                Math.Abs(value) >= 1000.0
                    ? "#,0"
                    : "0.##",
                CultureInfo.InvariantCulture
            );
        }
    }
}

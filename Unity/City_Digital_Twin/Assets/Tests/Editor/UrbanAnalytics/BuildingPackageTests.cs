using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UrbanAnalytics.Core.IO;
using UrbanAnalytics.Spatial.Geometry;
using UrbanAnalytics.UrbanContext;

namespace UrbanAnalytics.Tests
{
    /// <summary>
    /// EditMode tests for the building package (GBLD v3 reader,
    /// attribute table) and manifest-relative paths. The bytes are
    /// written here to the format documented in
    /// Src/pipelines/unity_package/buildings.py.
    /// </summary>
    public sealed class BuildingPackageTests
    {
        private const double OriginE = 355000.0;
        private const double OriginN = 6207500.0;


        private static void WriteRing(
            BinaryWriter w,
            params (float x, float z)[] vertices
        )
        {
            w.Write((uint)vertices.Length);

            foreach (var v in vertices)
            {
                w.Write(v.x);
                w.Write(v.z);
            }
        }


        private static void WriteId(
            BinaryWriter w,
            string id
        )
        {
            byte[] b = Encoding.UTF8.GetBytes(id);
            w.Write((ushort)b.Length);
            w.Write(b);
        }


        /// <summary>
        /// Two buildings: a courtyard block (exterior CCW + CW
        /// hole) with height, and a shed without height.
        /// </summary>
        private static byte[] SamplePackage()
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream, Encoding.UTF8);

            w.Write(Encoding.ASCII.GetBytes("GBLD"));
            w.Write(3u);
            w.Write(2u);
            w.Write(OriginE);
            w.Write(OriginN);

            WriteId(w, "building:a");
            w.Write(12.5f);
            w.Write(31.0f);
            w.Write((byte)1);
            w.Write((ushort)1);
            w.Write((ushort)2);
            WriteRing(w, (100, 100), (140, 100), (140, 140), (100, 140));
            WriteRing(w, (110, 110), (110, 130), (130, 130), (130, 110));

            WriteId(w, "building:b");
            w.Write(0f);
            w.Write(float.NaN);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)1);
            WriteRing(w, (0, 0), (3, 0), (3, 3), (0, 3));

            w.Flush();
            return stream.ToArray();
        }


        [Test]
        public void GeometryReader_ReadsRecordsHolesAndOrigin()
        {
            byte[] bytes = SamplePackage();

            Assert.IsTrue(BuildingGeometryReader.IsVersion3(bytes));

            var records = BuildingGeometryReader.Read(bytes, out double e, out double n);

            Assert.AreEqual(OriginE, e);
            Assert.AreEqual(OriginN, n);
            Assert.AreEqual(2, records.Count);

            BuildingRecord a = records[0];
            Assert.AreEqual("building:a", a.Id);
            Assert.AreEqual(12.5f, a.HeightMeters);
            Assert.IsTrue(a.HasHeight);
            Assert.AreEqual(1, a.Polygons.Count);
            Assert.AreEqual(1, a.Polygons[0].Holes.Count);
            Assert.AreEqual(OriginE + 100, a.Polygons[0].Exterior.Coordinates[0].Easting, 1e-6);
            Assert.AreEqual(RingOrientation.CounterClockwise, a.Polygons[0].Exterior.Orientation);
            Assert.AreEqual(RingOrientation.Clockwise, a.Polygons[0].Holes[0].Orientation);
            Assert.AreEqual(40 * 40 - 20 * 20, a.Polygons[0].Area, 1e-3);

            BuildingRecord b = records[1];
            Assert.IsFalse(b.HasHeight);
            Assert.IsTrue(float.IsNaN(b.GroundZ));
        }


        [Test]
        public void GeometryReader_RejectsTrailingBytes()
        {
            byte[] bytes = SamplePackage().Concat(new byte[] { 0 }).ToArray();

            Assert.Throws<InvalidDataException>(
                () => BuildingGeometryReader.Read(bytes, out _, out _)
            );
        }


        [Test]
        public void AttributeTable_FormatsAndSkipsMissingValues()
        {
            string json =
                "{\"unitIds\":[\"building:a\",\"building:b\"],\"columns\":[" +
                "{\"variableId\":\"name\",\"stringValues\":[\"Rådhuset\",\"\"],\"valid\":[true,false]}," +
                "{\"variableId\":\"house_number\",\"integerValues\":[3,7]}," +
                "{\"variableId\":\"height\",\"floatValues\":[12.5,0.0],\"valid\":[true,false]}," +
                "{\"variableId\":\"main_building\",\"booleanValues\":[true,false]}]}";

            BuildingFieldDefinition[] fields =
            {
                new BuildingFieldDefinition { id = "name", displayName = "Name", group = "Identity", valueType = "String" },
                new BuildingFieldDefinition { id = "house_number", displayName = "No.", group = "Identity", valueType = "Integer" },
                new BuildingFieldDefinition { id = "height", displayName = "Height", group = "Size", valueType = "Float", unit = "m" },
                new BuildingFieldDefinition { id = "main_building", displayName = "Main", group = "Identity", valueType = "Boolean" },
                new BuildingFieldDefinition { id = "not_in_file", displayName = "X", group = "X", valueType = "String" },
            };

            BuildingAttributeTable table = BuildingAttributeTable.Parse(json, fields);

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual(4, table.Fields.Count);   // not_in_file skipped

            var a = table.GetValues("building:a");
            CollectionAssert.AreEqual(
                new[] { "Rådhuset", "3", "12.5", "Yes" },
                a.Select(v => v.Text).ToArray()
            );
            Assert.AreEqual(12.5, a[2].Number);

            var b = table.GetValues("building:b");
            CollectionAssert.AreEqual(
                new[] { "house_number", "main_building" },
                b.Select(v => v.Field.id).ToArray()
            );

            Assert.IsFalse(table.Contains("building:zzz"));
        }


        [Test]
        public void AttributeTable_RejectsLengthMismatch()
        {
            string json =
                "{\"unitIds\":[\"building:a\",\"building:b\"],\"columns\":[" +
                "{\"variableId\":\"height\",\"floatValues\":[1.0]}]}";

            BuildingFieldDefinition[] fields =
            {
                new BuildingFieldDefinition { id = "height", valueType = "Float" }
            };

            Assert.Throws<InvalidDataException>(
                () => BuildingAttributeTable.Parse(json, fields)
            );
        }


        [Test]
        public void ManifestRelativePaths_ResolveInsideCityFolder()
        {
            Assert.AreEqual(
                "cities/helsingborg/urban_context/buildings/layer.json",
                RuntimeAssetReader.ResolveSiblingPath(
                    "cities/helsingborg/project_manifest.json",
                    "urban_context/buildings/layer.json"
                )
            );

            // A root manifest keeps paths unchanged (older packages).
            Assert.AreEqual(
                "spatial_layers/ruta_250/layer.json",
                RuntimeAssetReader.ResolveSiblingPath(
                    "project_manifest.json",
                    "spatial_layers/ruta_250/layer.json"
                )
            );
        }
    }
}

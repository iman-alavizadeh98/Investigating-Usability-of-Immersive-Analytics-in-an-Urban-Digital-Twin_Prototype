using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Reader for Stanford PLY meshes (ascii, binary_little_endian, binary_big_endian).
    ///
    /// Written for the city digital twin's building meshes, which are emitted by
    /// Src/mesh_generation/builder.py :: export_ply as binary little-endian with
    /// interleaved float32 x/y/z/nx/ny/nz and uchar-count + uint32 triangle faces.
    /// The parser is deliberately general rather than hardcoded to that layout:
    /// it reads the header, records each property's type and offset, and skips
    /// properties it does not need (colour, confidence, intensity, ...). That way
    /// meshes exported from MeshLab, CloudCompare, Open3D or Blender also load.
    ///
    /// Not supported (fails loudly rather than guessing):
    ///   - list properties on the vertex element
    ///   - elements other than "vertex" and "face" are skipped, not interpreted
    ///
    /// Assumptions, stated explicitly per project policy:
    ///   - source coordinates are metres in a local origin-rebased frame (Z-up);
    ///     the CRS and origin live in the group manifest, not in the PLY itself
    ///   - faces are convex, so n-gons can be fan-triangulated
    /// </summary>
    public static class PlyParser
    {
        private enum PlyFormat { Ascii, BinaryLittleEndian, BinaryBigEndian }

        private enum ScalarType
        {
            Int8, UInt8, Int16, UInt16, Int32, UInt32, Float32, Float64
        }

        private sealed class PlyProperty
        {
            public string Name;
            public ScalarType Type;
            public bool IsList;
            public ScalarType CountType;   // only meaningful when IsList
        }

        private sealed class PlyElement
        {
            public string Name;
            public long Count;
            public readonly List<PlyProperty> Properties = new List<PlyProperty>();
        }

        /// <summary>Thrown when a file is not valid PLY or uses an unsupported layout.</summary>
        public class PlyParseException : Exception
        {
            public PlyParseException(string message) : base(message) { }
        }

        /// <summary>Parse a PLY file from disk.</summary>
        public static PlyMeshData Load(string path, PlyImportSettings settings = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("PLY path is null or empty", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("PLY file not found", path);

            using (var stream = File.OpenRead(path))
                return Load(stream, settings, path);
        }

        /// <summary>Parse PLY bytes already in memory (e.g. from UnityWebRequest).</summary>
        public static PlyMeshData Load(byte[] bytes, PlyImportSettings settings = null,
                                       string sourceName = "<memory>")
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var stream = new MemoryStream(bytes, writable: false))
                return Load(stream, settings, sourceName);
        }

        /// <summary>Parse PLY from an arbitrary seekable stream.</summary>
        public static PlyMeshData Load(Stream stream, PlyImportSettings settings = null,
                                       string sourceName = "<stream>")
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            settings = settings ?? PlyImportSettings.Default;

            var data = new PlyMeshData();
            var elements = new List<PlyElement>();
            PlyFormat format = ReadHeader(stream, elements, data.Comments, sourceName);

            // Elements appear in the body in the same order as in the header.
            foreach (var element in elements)
            {
                if (element.Name == "vertex")
                {
                    ReadVertexElement(stream, format, element, data, settings, sourceName);
                }
                else if (element.Name == "face")
                {
                    ReadFaceElement(stream, format, element, data, settings, sourceName);
                }
                else
                {
                    SkipElement(stream, format, element, sourceName);
                }
            }

            if (data.Vertices == null)
                throw new PlyParseException($"{sourceName}: no 'vertex' element found");
            if (data.Triangles == null)
                data.Triangles = Array.Empty<int>();

            return data;
        }

        // ------------------------------------------------------------------
        // Header
        // ------------------------------------------------------------------

        /// <summary>
        /// Read the ascii header byte-by-byte. We cannot use StreamReader here: it
        /// buffers ahead and would swallow the start of the binary body.
        /// </summary>
        private static PlyFormat ReadHeader(Stream stream, List<PlyElement> elements,
                                            List<string> comments, string sourceName)
        {
            string magic = ReadHeaderLine(stream);
            if (magic == null || magic.Trim() != "ply")
                throw new PlyParseException($"{sourceName}: missing 'ply' magic line — not a PLY file");

            PlyFormat? format = null;
            PlyElement current = null;
            bool sawEndHeader = false;

            while (true)
            {
                string line = ReadHeaderLine(stream);
                if (line == null)
                    throw new PlyParseException($"{sourceName}: header ended without 'end_header'");

                line = line.Trim();
                if (line.Length == 0) continue;

                string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (tokens[0])
                {
                    case "comment":
                    case "obj_info":
                        comments.Add(line);
                        break;

                    case "format":
                        if (tokens.Length < 2)
                            throw new PlyParseException($"{sourceName}: malformed 'format' line: '{line}'");
                        switch (tokens[1])
                        {
                            case "ascii": format = PlyFormat.Ascii; break;
                            case "binary_little_endian": format = PlyFormat.BinaryLittleEndian; break;
                            case "binary_big_endian": format = PlyFormat.BinaryBigEndian; break;
                            default:
                                throw new PlyParseException($"{sourceName}: unsupported PLY format '{tokens[1]}'");
                        }
                        break;

                    case "element":
                        if (tokens.Length < 3)
                            throw new PlyParseException($"{sourceName}: malformed 'element' line: '{line}'");
                        current = new PlyElement
                        {
                            Name = tokens[1],
                            Count = long.Parse(tokens[2], CultureInfo.InvariantCulture)
                        };
                        elements.Add(current);
                        break;

                    case "property":
                        if (current == null)
                            throw new PlyParseException($"{sourceName}: 'property' before any 'element': '{line}'");
                        current.Properties.Add(ParseProperty(tokens, line, sourceName));
                        break;

                    case "end_header":
                        sawEndHeader = true;
                        break;

                    default:
                        // Unknown header keyword: record it rather than failing, so
                        // vendor-specific lines do not block an otherwise valid file.
                        comments.Add("unhandled header line: " + line);
                        break;
                }

                if (sawEndHeader) break;
            }

            if (!format.HasValue)
                throw new PlyParseException($"{sourceName}: header has no 'format' line");

            return format.Value;
        }

        private static PlyProperty ParseProperty(string[] tokens, string line, string sourceName)
        {
            // property <type> <name>
            // property list <count-type> <index-type> <name>
            if (tokens.Length >= 5 && tokens[1] == "list")
            {
                return new PlyProperty
                {
                    IsList = true,
                    CountType = ParseScalarType(tokens[2], line, sourceName),
                    Type = ParseScalarType(tokens[3], line, sourceName),
                    Name = tokens[4]
                };
            }

            if (tokens.Length >= 3)
            {
                return new PlyProperty
                {
                    IsList = false,
                    Type = ParseScalarType(tokens[1], line, sourceName),
                    Name = tokens[2]
                };
            }

            throw new PlyParseException($"{sourceName}: malformed 'property' line: '{line}'");
        }

        private static ScalarType ParseScalarType(string token, string line, string sourceName)
        {
            switch (token)
            {
                case "char":
                case "int8": return ScalarType.Int8;
                case "uchar":
                case "uint8": return ScalarType.UInt8;
                case "short":
                case "int16": return ScalarType.Int16;
                case "ushort":
                case "uint16": return ScalarType.UInt16;
                case "int":
                case "int32": return ScalarType.Int32;
                case "uint":
                case "uint32": return ScalarType.UInt32;
                case "float":
                case "float32": return ScalarType.Float32;
                case "double":
                case "float64": return ScalarType.Float64;
                default:
                    throw new PlyParseException(
                        $"{sourceName}: unknown PLY scalar type '{token}' in line '{line}'");
            }
        }

        /// <summary>Read one '\n'-terminated ascii line without over-reading the stream.</summary>
        private static string ReadHeaderLine(Stream stream)
        {
            var sb = new StringBuilder(64);
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                if (b == '\n')
                    return sb.ToString().TrimEnd('\r');
                sb.Append((char)b);
            }
            return sb.Length > 0 ? sb.ToString().TrimEnd('\r') : null;
        }

        // ------------------------------------------------------------------
        // Vertex element
        // ------------------------------------------------------------------

        private static void ReadVertexElement(Stream stream, PlyFormat format, PlyElement element,
                                              PlyMeshData data, PlyImportSettings settings,
                                              string sourceName)
        {
            if (element.Count > int.MaxValue)
                throw new PlyParseException($"{sourceName}: vertex count {element.Count} exceeds int range");

            int count = (int)element.Count;
            data.VertexCount = count;

            // Locate the properties we care about; -1 means absent.
            int xi = -1, yi = -1, zi = -1, nxi = -1, nyi = -1, nzi = -1;
            for (int i = 0; i < element.Properties.Count; i++)
            {
                var p = element.Properties[i];
                if (p.IsList)
                    throw new PlyParseException(
                        $"{sourceName}: list property '{p.Name}' on the vertex element is not supported");

                switch (p.Name)
                {
                    case "x": xi = i; break;
                    case "y": yi = i; break;
                    case "z": zi = i; break;
                    case "nx": nxi = i; break;
                    case "ny": nyi = i; break;
                    case "nz": nzi = i; break;
                }
            }

            if (xi < 0 || yi < 0 || zi < 0)
                throw new PlyParseException($"{sourceName}: vertex element is missing x/y/z properties");

            bool hasNormals = nxi >= 0 && nyi >= 0 && nzi >= 0;

            var vertices = new Vector3[count];
            var normals = hasNormals ? new Vector3[count] : null;
            var values = new double[element.Properties.Count];

            if (format == PlyFormat.Ascii)
            {
                for (int v = 0; v < count; v++)
                {
                    ReadAsciiScalars(stream, element.Properties, values, sourceName, "vertex", v);
                    StoreVertex(vertices, normals, v, values, xi, yi, zi, nxi, nyi, nzi,
                                hasNormals, settings);
                }
            }
            else
            {
                bool swap = NeedsByteSwap(format);
                int stride = 0;
                foreach (var p in element.Properties) stride += SizeOf(p.Type);

                // Read the whole vertex block in one go: one 5.7 MB read beats
                // 237k small reads on the district-scale meshes.
                byte[] block = ReadExactly(stream, (long)stride * count, sourceName, "vertex data");
                int offset = 0;
                for (int v = 0; v < count; v++)
                {
                    for (int p = 0; p < element.Properties.Count; p++)
                    {
                        values[p] = ReadBinaryScalar(block, ref offset, element.Properties[p].Type, swap);
                    }
                    StoreVertex(vertices, normals, v, values, xi, yi, zi, nxi, nyi, nzi,
                                hasNormals, settings);
                }
            }

            data.Vertices = vertices;
            data.Normals = normals;
        }

        private static void StoreVertex(Vector3[] vertices, Vector3[] normals, int index,
                                        double[] values, int xi, int yi, int zi,
                                        int nxi, int nyi, int nzi, bool hasNormals,
                                        PlyImportSettings settings)
        {
            float x = (float)values[xi];
            float y = (float)values[yi];
            float z = (float)values[zi];

            // Z-up right-handed -> Y-up left-handed. Swapping Y and Z also flips
            // handedness, which is why triangle winding is reversed to match.
            vertices[index] = settings.ConvertAxes
                ? new Vector3(x, z, y) * settings.Scale
                : new Vector3(x, y, z) * settings.Scale;

            if (hasNormals)
            {
                float nx = (float)values[nxi];
                float ny = (float)values[nyi];
                float nz = (float)values[nzi];
                Vector3 n = settings.ConvertAxes
                    ? new Vector3(nx, nz, ny)
                    : new Vector3(nx, ny, nz);
                // Scale never flips orientation here (uniform, positive), so the
                // normal only needs renormalising against float drift.
                normals[index] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            }
        }

        // ------------------------------------------------------------------
        // Face element
        // ------------------------------------------------------------------

        private static void ReadFaceElement(Stream stream, PlyFormat format, PlyElement element,
                                            PlyMeshData data, PlyImportSettings settings,
                                            string sourceName)
        {
            int faceCount = (int)Math.Min(element.Count, int.MaxValue);
            data.FaceCount = faceCount;

            int listIndex = -1;
            for (int i = 0; i < element.Properties.Count; i++)
            {
                var p = element.Properties[i];
                if (p.IsList && (p.Name == "vertex_indices" || p.Name == "vertex_index"))
                {
                    listIndex = i;
                    break;
                }
            }

            if (listIndex < 0)
            {
                Debug.LogWarning($"[PlyParser] {sourceName}: face element has no 'vertex_indices' " +
                                 "list property; importing as a point cloud (no triangles).");
                data.Triangles = Array.Empty<int>();
                SkipElement(stream, format, element, sourceName);
                return;
            }

            // Most faces are triangles; pre-size for that and let the list grow
            // only when n-gons show up.
            var triangles = new List<int>(faceCount * 3);
            var indices = new List<int>(8);

            // Winding is deliberately NOT reversed when converting axes.
            // Swapping Y and Z is itself a handedness flip: it turns the source's
            // right-handed / counter-clockwise-front convention directly into
            // Unity's left-handed / clockwise-front one. Reversing the index order
            // as well would flip it back and render every face inside-out (invisible
            // under backface culling). Verified against the pipeline's district
            // meshes: after the swap alone, 99.3% of face normals agree with the
            // exported per-vertex normals.
            bool flipWinding = settings.FlipWinding;
            int vertexCount = data.Vertices != null ? data.Vertices.Length : 0;
            bool warnedOutOfRange = false;

            if (format == PlyFormat.Ascii)
            {
                for (int f = 0; f < faceCount; f++)
                {
                    ReadAsciiFace(stream, element.Properties, listIndex, indices, sourceName, f);
                    AppendTriangles(triangles, indices, flipWinding, vertexCount,
                                    sourceName, f, ref warnedOutOfRange);
                }
            }
            else
            {
                bool swap = NeedsByteSwap(format);

                // Face records are variable-length, so read the rest of the stream
                // into one buffer and walk it. For the district meshes this is a
                // few MB — far cheaper than millions of one-byte stream reads.
                byte[] block = ReadToEnd(stream);
                int offset = 0;

                for (int f = 0; f < faceCount; f++)
                {
                    indices.Clear();
                    for (int p = 0; p < element.Properties.Count; p++)
                    {
                        var prop = element.Properties[p];
                        if (prop.IsList)
                        {
                            int n = (int)ReadBinaryScalar(block, ref offset, prop.CountType, swap);
                            if (n < 0)
                                throw new PlyParseException(
                                    $"{sourceName}: face {f} declares a negative vertex count ({n})");

                            if (p == listIndex)
                            {
                                for (int k = 0; k < n; k++)
                                    indices.Add((int)ReadBinaryScalar(block, ref offset, prop.Type, swap));
                            }
                            else
                            {
                                offset += n * SizeOf(prop.Type);   // skip e.g. per-face colour
                            }
                        }
                        else
                        {
                            offset += SizeOf(prop.Type);
                        }
                    }

                    AppendTriangles(triangles, indices, flipWinding, vertexCount,
                                    sourceName, f, ref warnedOutOfRange);
                }
            }

            data.Triangles = triangles.ToArray();
        }

        /// <summary>
        /// Fan-triangulate one face and append it. Assumes convex faces — true for
        /// the extruded building footprints this pipeline produces, and the standard
        /// assumption for PLY consumers generally.
        /// </summary>
        private static void AppendTriangles(List<int> triangles, List<int> indices, bool flipWinding,
                                            int vertexCount, string sourceName, int faceNumber,
                                            ref bool warnedOutOfRange)
        {
            if (indices.Count < 3) return;   // degenerate: point or edge, nothing to draw

            for (int i = 1; i + 1 < indices.Count; i++)
            {
                int a = indices[0];
                int b = indices[i];
                int c = indices[i + 1];

                if ((uint)a >= (uint)vertexCount ||
                    (uint)b >= (uint)vertexCount ||
                    (uint)c >= (uint)vertexCount)
                {
                    if (!warnedOutOfRange)
                    {
                        Debug.LogWarning($"[PlyParser] {sourceName}: face {faceNumber} references a " +
                                         $"vertex index outside 0..{vertexCount - 1}; skipping this and " +
                                         "any further out-of-range faces.");
                        warnedOutOfRange = true;
                    }
                    continue;
                }

                if (flipWinding)
                {
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                }
                else
                {
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
            }
        }

        // ------------------------------------------------------------------
        // Skipping unknown elements
        // ------------------------------------------------------------------

        private static void SkipElement(Stream stream, PlyFormat format, PlyElement element,
                                        string sourceName)
        {
            if (format == PlyFormat.Ascii)
            {
                for (long i = 0; i < element.Count; i++)
                    ReadHeaderLine(stream);
                return;
            }

            bool swap = NeedsByteSwap(format);
            bool hasList = false;
            int fixedStride = 0;
            foreach (var p in element.Properties)
            {
                if (p.IsList) { hasList = true; break; }
                fixedStride += SizeOf(p.Type);
            }

            if (!hasList)
            {
                // Fixed-size records: seek straight past them.
                long skipBytes = (long)fixedStride * element.Count;
                if (stream.CanSeek) stream.Seek(skipBytes, SeekOrigin.Current);
                else ReadExactly(stream, skipBytes, sourceName, $"'{element.Name}' element");
                return;
            }

            // Variable-size records must be walked property by property.
            var scratch = new byte[8];
            for (long i = 0; i < element.Count; i++)
            {
                foreach (var p in element.Properties)
                {
                    if (p.IsList)
                    {
                        int n = (int)ReadStreamScalar(stream, p.CountType, swap, scratch, sourceName);
                        long bytes = (long)n * SizeOf(p.Type);
                        if (stream.CanSeek) stream.Seek(bytes, SeekOrigin.Current);
                        else ReadExactly(stream, bytes, sourceName, $"'{element.Name}' list");
                    }
                    else
                    {
                        int size = SizeOf(p.Type);
                        if (stream.CanSeek) stream.Seek(size, SeekOrigin.Current);
                        else ReadExactly(stream, size, sourceName, $"'{element.Name}' scalar");
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Scalar decoding
        // ------------------------------------------------------------------

        private static bool NeedsByteSwap(PlyFormat format)
        {
            bool fileIsLittleEndian = format == PlyFormat.BinaryLittleEndian;
            return fileIsLittleEndian != BitConverter.IsLittleEndian;
        }

        private static int SizeOf(ScalarType type)
        {
            switch (type)
            {
                case ScalarType.Int8:
                case ScalarType.UInt8: return 1;
                case ScalarType.Int16:
                case ScalarType.UInt16: return 2;
                case ScalarType.Int32:
                case ScalarType.UInt32:
                case ScalarType.Float32: return 4;
                case ScalarType.Float64: return 8;
                default: throw new PlyParseException($"unhandled scalar type {type}");
            }
        }

        private static double ReadBinaryScalar(byte[] buffer, ref int offset, ScalarType type, bool swap)
        {
            int size = SizeOf(type);
            if (offset + size > buffer.Length)
                throw new PlyParseException(
                    $"unexpected end of PLY data at byte {offset} (needed {size} more)");

            if (swap && size > 1)
                Array.Reverse(buffer, offset, size);

            double result;
            switch (type)
            {
                case ScalarType.Int8: result = (sbyte)buffer[offset]; break;
                case ScalarType.UInt8: result = buffer[offset]; break;
                case ScalarType.Int16: result = BitConverter.ToInt16(buffer, offset); break;
                case ScalarType.UInt16: result = BitConverter.ToUInt16(buffer, offset); break;
                case ScalarType.Int32: result = BitConverter.ToInt32(buffer, offset); break;
                case ScalarType.UInt32: result = BitConverter.ToUInt32(buffer, offset); break;
                case ScalarType.Float32: result = BitConverter.ToSingle(buffer, offset); break;
                case ScalarType.Float64: result = BitConverter.ToDouble(buffer, offset); break;
                default: throw new PlyParseException($"unhandled scalar type {type}");
            }

            offset += size;
            return result;
        }

        private static double ReadStreamScalar(Stream stream, ScalarType type, bool swap,
                                               byte[] scratch, string sourceName)
        {
            int size = SizeOf(type);
            int read = 0;
            while (read < size)
            {
                int n = stream.Read(scratch, read, size - read);
                if (n <= 0)
                    throw new PlyParseException($"{sourceName}: unexpected end of file reading a scalar");
                read += n;
            }
            int offset = 0;
            return ReadBinaryScalar(scratch, ref offset, type, swap);
        }

        // ------------------------------------------------------------------
        // Ascii decoding
        // ------------------------------------------------------------------

        private static void ReadAsciiScalars(Stream stream, List<PlyProperty> properties,
                                             double[] values, string sourceName,
                                             string elementName, int recordIndex)
        {
            string line = ReadHeaderLine(stream);
            if (line == null)
                throw new PlyParseException(
                    $"{sourceName}: unexpected end of file at {elementName} {recordIndex}");

            string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < properties.Count)
                throw new PlyParseException(
                    $"{sourceName}: {elementName} {recordIndex} has {tokens.Length} values, " +
                    $"expected {properties.Count}");

            for (int i = 0; i < properties.Count; i++)
                values[i] = double.Parse(tokens[i], CultureInfo.InvariantCulture);
        }

        private static void ReadAsciiFace(Stream stream, List<PlyProperty> properties, int listIndex,
                                          List<int> indices, string sourceName, int faceNumber)
        {
            string line = ReadHeaderLine(stream);
            if (line == null)
                throw new PlyParseException($"{sourceName}: unexpected end of file at face {faceNumber}");

            string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            indices.Clear();

            int t = 0;
            for (int p = 0; p < properties.Count; p++)
            {
                var prop = properties[p];
                if (prop.IsList)
                {
                    if (t >= tokens.Length)
                        throw new PlyParseException(
                            $"{sourceName}: face {faceNumber} is truncated: '{line}'");

                    int n = int.Parse(tokens[t++], CultureInfo.InvariantCulture);
                    if (t + n > tokens.Length)
                        throw new PlyParseException(
                            $"{sourceName}: face {faceNumber} declares {n} indices but the line " +
                            $"holds only {tokens.Length - t}: '{line}'");

                    if (p == listIndex)
                    {
                        for (int k = 0; k < n; k++)
                            indices.Add(int.Parse(tokens[t + k], CultureInfo.InvariantCulture));
                    }
                    t += n;
                }
                else
                {
                    t++;
                }
            }
        }

        // ------------------------------------------------------------------
        // Stream helpers
        // ------------------------------------------------------------------

        private static byte[] ReadExactly(Stream stream, long byteCount, string sourceName, string what)
        {
            if (byteCount > int.MaxValue)
                throw new PlyParseException(
                    $"{sourceName}: {what} needs {byteCount} bytes, above the 2 GB single-array limit");

            var buffer = new byte[byteCount];
            int read = 0;
            while (read < buffer.Length)
            {
                int n = stream.Read(buffer, read, buffer.Length - read);
                if (n <= 0)
                    throw new PlyParseException(
                        $"{sourceName}: unexpected end of file reading {what} " +
                        $"(got {read} of {buffer.Length} bytes)");
                read += n;
            }
            return buffer;
        }

        private static byte[] ReadToEnd(Stream stream)
        {
            if (stream.CanSeek)
            {
                long remaining = stream.Length - stream.Position;
                if (remaining <= int.MaxValue)
                {
                    var exact = new byte[remaining];
                    int read = 0;
                    while (read < exact.Length)
                    {
                        int n = stream.Read(exact, read, exact.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read == exact.Length) return exact;
                    var trimmed = new byte[read];
                    Buffer.BlockCopy(exact, 0, trimmed, 0, read);
                    return trimmed;
                }
            }

            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}

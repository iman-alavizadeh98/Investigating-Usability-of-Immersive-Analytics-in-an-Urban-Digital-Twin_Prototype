using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Parsed form of the mesh pipeline's <c>meshes_manifest.json</c>.
    ///
    /// This file — not the PLY — is what carries georeferencing. Each PLY is written
    /// in a local frame rebased on its own origin, so the manifest's <c>origin</c>
    /// per group is the only thing that can put districts back in the right place
    /// relative to each other.
    ///
    /// Written against the schema produced by the 2026-08-03 export refactor
    /// (`primary_format` + `mesh_files`), with fallbacks to the older
    /// `ply_files` / `glb_files` views so pre-refactor manifests still load.
    /// </summary>
    [Serializable]
    public class MeshManifest
    {
        public string timestamp;
        public string strategy;
        public string strategy_description;
        public string crs;                  // e.g. "EPSG:3006" (SWEREF99 TM)
        public int total_groups;
        public long total_vertices;
        public long total_triangles;

        /// <summary>
        /// Frozen analytical lattice this run was partitioned on. Present for
        /// grid-like strategies; null for older manifests and non-grid runs.
        ///
        /// Its `anchor_x`/`anchor_y` are the STABLE world origin: using them keeps
        /// Unity world coordinates identical across regenerated runs, which is what
        /// makes interaction logs comparable between evaluation sessions. Deriving
        /// the origin from a run's own extent instead shifts every coordinate
        /// whenever the data is rebuilt.
        /// </summary>
        public GridReference grid_reference;

        public List<MeshGroupEntry> meshes = new List<MeshGroupEntry>();

        /// <summary>Directory the manifest was loaded from; mesh paths resolve against it.</summary>
        [NonSerialized] public string ManifestDirectory;

        public static MeshManifest Load(string manifestPath)
        {
            string resolved = ProjectPaths.Resolve(manifestPath);
            if (!File.Exists(resolved))
                throw new FileNotFoundException(
                    $"Mesh manifest not found at '{resolved}'. It is written next to the " +
                    "mesh output folder by run_mesh_generation.py.", resolved);

            string json = File.ReadAllText(resolved);

            // JsonUtility cannot handle the nested dictionaries in `mesh_files`
            // ({lod: {fmt: filename}}), so those are recovered separately below.
            MeshManifest manifest = JsonUtility.FromJson<MeshManifest>(json);
            if (manifest == null)
                throw new InvalidDataException($"Could not parse manifest JSON at '{resolved}'");

            manifest.ManifestDirectory = Path.GetDirectoryName(resolved);
            manifest.ResolveMeshFilenames(json);
            return manifest;
        }

        /// <summary>
        /// JsonUtility drops dictionary-shaped fields, so pull each group's PLY
        /// filename straight out of the raw JSON. Prefers the authoritative
        /// `mesh_files` map, falling back to `ply_files`, then to the conventional
        /// "<group_id>_<lod>.ply" name.
        /// </summary>
        private void ResolveMeshFilenames(string json)
        {
            foreach (var group in meshes)
            {
                if (group == null || string.IsNullOrEmpty(group.group_id)) continue;

                string lod = (group.lod_levels != null && group.lod_levels.Count > 0)
                    ? group.lod_levels[0]
                    : "lod1";

                string found = MeshManifestJsonHelper.FindPlyFilename(json, group.group_id);
                group.ResolvedPlyFilename = !string.IsNullOrEmpty(found)
                    ? found
                    : $"{group.group_id}_{lod}.ply";
            }
        }

        /// <summary>
        /// Absolute path to a group's primary PLY. Mesh files live in a subfolder
        /// next to the manifest — "mesh_files/" since the 2026-08-03 refactor,
        /// "glb_files/" before it; both are probed.
        /// </summary>
        public string GetPlyPath(MeshGroupEntry group)
        {
            if (group == null) return null;
            string file = group.ResolvedPlyFilename;
            if (string.IsNullOrEmpty(file)) return null;

            foreach (string sub in new[] { "mesh_files", "glb_files", "" })
            {
                string candidate = Path.Combine(ManifestDirectory ?? "", sub, file);
                if (File.Exists(candidate)) return candidate;
            }
            return Path.Combine(ManifestDirectory ?? "", "mesh_files", file);
        }

        /// <summary>
        /// The world anchor to rebase scene coordinates on.
        ///
        /// Prefers the manifest's FROZEN lattice anchor, so Unity world coordinates
        /// stay identical across regenerated runs — essential for comparing
        /// interaction logs between evaluation sessions. Falls back to the run's
        /// south-west corner for older manifests that predate the frozen lattice.
        /// </summary>
        /// <param name="usedFrozenAnchor">True when the stable anchor was available.</param>
        public Vector2 ResolveWorldAnchor(out bool usedFrozenAnchor)
        {
            if (grid_reference != null && grid_reference.IsValid)
            {
                usedFrozenAnchor = true;
                return new Vector2((float)grid_reference.anchor_x, (float)grid_reference.anchor_y);
            }

            usedFrozenAnchor = false;
            return ComputeSouthWestOrigin();
        }

        /// <summary>
        /// The south-west corner across all groups, in CRS units. Using it as the
        /// scene origin keeps every district at positive, modest coordinates
        /// instead of the ~300 000 / 6 400 000 m raw SWEREF99 values, which would
        /// destroy float precision in the renderer.
        ///
        /// NOTE: this value depends on which groups the run contains, so it MOVES
        /// when the data is regenerated. Prefer <see cref="ResolveWorldAnchor"/>.
        /// </summary>
        public Vector2 ComputeSouthWestOrigin()
        {
            if (meshes == null || meshes.Count == 0) return Vector2.zero;

            double minX = double.MaxValue, minY = double.MaxValue;
            foreach (var g in meshes)
            {
                if (g?.origin == null) continue;
                minX = Math.Min(minX, g.origin.x);
                minY = Math.Min(minY, g.origin.y);
            }
            if (minX == double.MaxValue) return Vector2.zero;
            return new Vector2((float)minX, (float)minY);
        }
    }

    [Serializable]
    public class MeshGroupEntry
    {
        public string group_id;
        public string group_name;
        public List<string> lod_levels = new List<string>();
        public string primary_format;
        public string crs;
        /// <summary>
        /// Local frame origin: the CONTENT bounding-box corner the mesh vertices
        /// were rebased on. This is what places the mesh — do not confuse it with
        /// <see cref="cell"/>, which is the nominal lattice corner.
        /// </summary>
        public MeshOrigin origin;

        /// <summary>
        /// Lattice address of this group. Its <c>origin_x/origin_y</c> is the exact
        /// cell corner and is the key that joins to the cell-attribute layers
        /// (population, employment, …). Null for non-grid strategies.
        /// </summary>
        public MeshCell cell;

        /// <summary>How buildings were assigned to groups, e.g. "representative_point".</summary>
        public string assignment_rule;

        public MeshBounds bounds_epsg3006;
        public int triangle_count;
        public int vertex_count;
        public float file_size_mb;

        /// <summary>Filename recovered from the manifest's dictionary-shaped fields.</summary>
        [NonSerialized] public string ResolvedPlyFilename;
    }

    /// <summary>
    /// The frozen lattice definition written by Src/mesh_generation/grid_reference.py.
    /// Lets the runtime reconstruct any cell's bounds and, more importantly, adopt a
    /// world anchor that does not move when the data is regenerated.
    /// </summary>
    [Serializable]
    public class GridReference
    {
        public double anchor_x;
        public double anchor_y;
        public int cell_size_m;
        public string crs;
        public string assignment_rule;   // e.g. "representative_point"
        public string id_format;         // e.g. "grid_{col:+04d}_{row:+04d}"

        /// <summary>True once a manifest actually carried an anchor.</summary>
        public bool IsValid => cell_size_m > 0 && (anchor_x != 0.0 || anchor_y != 0.0);
    }

    /// <summary>Lattice address of a group, when the strategy is grid-like.</summary>
    [Serializable]
    public class MeshCell
    {
        public int col;
        public int row;
        public double origin_x;   // exact lattice corner (NOT the content bbox corner)
        public double origin_y;
        public int size_m;
    }

    [Serializable]
    public class MeshOrigin
    {
        public double x;    // easting the group's vertices were rebased on
        public double y;    // northing the group's vertices were rebased on
        public double z;    // height datum — NOT subtracted from vertices (see below)
    }

    [Serializable]
    public class MeshBounds
    {
        public double west, south, east, north;
    }

    /// <summary>
    /// Minimal, allocation-light scan for a group's PLY filename inside the raw
    /// manifest JSON. A full JSON parser would be overkill for one nested lookup,
    /// and Unity ships no dictionary-capable serializer.
    /// </summary>
    internal static class MeshManifestJsonHelper
    {
        public static string FindPlyFilename(string json, string groupId)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(groupId)) return null;

            // Anchor on this group's object, then take the first *.ply string after
            // it — that is its mesh_files / ply_files entry.
            int anchor = json.IndexOf($"\"group_id\": \"{groupId}\"", StringComparison.Ordinal);
            if (anchor < 0)
                anchor = json.IndexOf($"\"group_id\":\"{groupId}\"", StringComparison.Ordinal);
            if (anchor < 0) return null;

            // Stop at the next group so we never borrow a neighbour's filename.
            int next = json.IndexOf("\"group_id\"", anchor + 10, StringComparison.Ordinal);
            int end = next < 0 ? json.Length : next;

            int ply = json.IndexOf(".ply\"", anchor, StringComparison.Ordinal);
            if (ply < 0 || ply > end) return null;

            int quote = json.LastIndexOf('"', ply);
            if (quote < 0) return null;

            return json.Substring(quote + 1, (ply + 4) - (quote + 1));
        }
    }
}

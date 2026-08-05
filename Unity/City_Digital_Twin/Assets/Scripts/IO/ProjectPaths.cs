using System.IO;
using UnityEngine;

namespace CityDigitalTwin.IO
{
    /// <summary>
    /// Resolves data paths that point outside the Unity project.
    ///
    /// The mesh pipeline writes to <c>Processed_data/</c> at the repo root, which is
    /// outside <c>Assets/</c> and therefore invisible to Unity's asset system. Paths
    /// to it are stored relative to the Unity **project root** (the folder containing
    /// <c>Assets/</c>, i.e. <c>Unity/City_Digital_Twin/</c>) so scenes and configs
    /// stay portable across machines and checkouts.
    /// </summary>
    public static class ProjectPaths
    {
        /// <summary>
        /// Turn an absolute path, or one relative to the Unity project root, into an
        /// absolute path. Returns null for empty input.
        /// </summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        /// <summary>
        /// The folder containing <c>Assets/</c>. In a build, <c>Application.dataPath</c>
        /// points at the data folder instead, so external paths only make sense in the
        /// editor or alongside a deployed data directory.
        /// </summary>
        public static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        /// <summary>Repo-root <c>Processed_data/</c>, or null if it is not where expected.</summary>
        public static string ProcessedDataDirectory
        {
            get
            {
                string candidate = Resolve("../../Processed_data");
                return Directory.Exists(candidate) ? candidate : null;
            }
        }
    }
}

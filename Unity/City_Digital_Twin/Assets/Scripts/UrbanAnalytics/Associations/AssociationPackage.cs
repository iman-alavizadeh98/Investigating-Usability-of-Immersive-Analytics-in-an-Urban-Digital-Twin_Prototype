using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using UrbanAnalytics.Core;
using UrbanAnalytics.Core.IO;

namespace UrbanAnalytics.Associations
{
    /// <summary>
    /// associations/&lt;id&gt;/layer.json of a city package
    /// (written by Src/pipelines/unity_package/layer_files.py).
    /// </summary>
    [Serializable]
    public sealed class AssociationDefinition
    {
        public string schemaVersion;
        public string id;
        public string description;
        public string sourceLayerId;
        public string targetLayerId;
        public string pairsFile;
        public int pairCount;
    }


    /// <summary>
    /// associations/&lt;id&gt;/pairs.json: sourceIds[i] → targetIds[i].
    /// </summary>
    [Serializable]
    public sealed class AssociationPairsDto
    {
        public string schemaVersion;
        public string associationId;
        public string[] sourceIds;
        public string[] targetIds;
    }


    /// <summary>
    /// Loads every association listed in the project manifest into the
    /// AssociationManager (e.g. buildings_to_ruta, buildings_to_deso,
    /// buildings_to_valdistrikt). Each set replaces any earlier set with the
    /// same id.
    /// </summary>
    public static class AssociationPackageLoader
    {
        public static async Task<IReadOnlyDictionary<string, int>> LoadAsync(
            ProjectManager project,
            AssociationManager associations,
            CancellationToken cancellationToken
        )
        {
            var counts =
                new Dictionary<string, int>(
                    StringComparer.Ordinal
                );

            ResourceReference[] references =
                project.Manifest?.associations;

            if (references == null ||
                references.Length == 0)
            {
                return counts;
            }


            foreach (ResourceReference reference in references)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reference == null ||
                    string.IsNullOrWhiteSpace(reference.definition))
                {
                    continue;
                }


                string layerPath =
                    project.ResolvePackagePath(
                        reference.definition
                    );

                string layerJson =
                    await project.AssetReader.ReadTextAsync(
                        layerPath,
                        cancellationToken
                    );

                AssociationDefinition definition =
                    JsonUtility.FromJson<AssociationDefinition>(
                        layerJson
                    );

                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.pairsFile))
                {
                    throw new InvalidDataException(
                        $"'{layerPath}' does not name its pairs file."
                    );
                }


                string associationId =
                    string.IsNullOrWhiteSpace(definition.id)
                        ? reference.id
                        : definition.id;

                string pairsJson =
                    await project.AssetReader.ReadTextAsync(
                        RuntimeAssetReader.ResolveSiblingPath(
                            layerPath,
                            definition.pairsFile
                        ),
                        cancellationToken
                    );

                // JsonUtility is safe off the main thread.
                AssociationPairsDto pairs =
                    await Task.Run(
                        () => JsonUtility.FromJson<AssociationPairsDto>(
                            pairsJson
                        ),
                        cancellationToken
                    );

                if (pairs?.sourceIds == null ||
                    pairs.targetIds == null ||
                    pairs.sourceIds.Length != pairs.targetIds.Length)
                {
                    throw new InvalidDataException(
                        $"Association '{associationId}': source and " +
                        $"target lists are missing or differ in length."
                    );
                }


                associations.ClearAssociation(
                    associationId
                );

                for (int i = 0; i < pairs.sourceIds.Length; i++)
                {
                    associations.Register(
                        associationId,
                        pairs.sourceIds[i],
                        pairs.targetIds[i]
                    );
                }


                counts[associationId] =
                    pairs.sourceIds.Length;
            }


            return counts;
        }
    }
}

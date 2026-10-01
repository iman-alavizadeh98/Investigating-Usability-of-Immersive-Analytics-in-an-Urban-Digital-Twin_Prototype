using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanAnalytics.Associations
{
    /// <summary>
    /// Stores semantic relationships between runtime entities.
    ///
    /// Example:
    ///
    /// association:
    ///     buildings_to_ruta
    ///
    /// building:1234
    ///     ->
    /// ruta_250:3175006390000
    ///
    /// This manager stores relationships only.
    /// It does not own geometry or analytical values.
    /// </summary>
    public sealed class AssociationManager : MonoBehaviour
    {
        private readonly Dictionary<
            string,
            Dictionary<string, string>
        > associations =
            new Dictionary<
                string,
                Dictionary<string, string>
            >(
                StringComparer.Ordinal
            );


        public int AssociationSetCount =>
            associations.Count;


        public void Register(
            string associationId,
            string sourceId,
            string targetId
        )
        {
            string normalizedAssociationId =
                NormalizeRequired(
                    associationId,
                    nameof(associationId)
                );

            string normalizedSourceId =
                NormalizeRequired(
                    sourceId,
                    nameof(sourceId)
                );

            string normalizedTargetId =
                NormalizeRequired(
                    targetId,
                    nameof(targetId)
                );


            if (!associations.TryGetValue(
                    normalizedAssociationId,
                    out Dictionary<string, string> map
                ))
            {
                map =
                    new Dictionary<string, string>(
                        StringComparer.Ordinal
                    );

                associations.Add(
                    normalizedAssociationId,
                    map
                );
            }


            if (map.TryGetValue(
                    normalizedSourceId,
                    out string existingTarget
                ))
            {
                if (!string.Equals(
                        existingTarget,
                        normalizedTargetId,
                        StringComparison.Ordinal
                    ))
                {
                    throw new InvalidOperationException(
                        $"Association '{normalizedAssociationId}' " +
                        $"already maps '{normalizedSourceId}' to " +
                        $"'{existingTarget}', so it cannot also map " +
                        $"to '{normalizedTargetId}'."
                    );
                }

                return;
            }


            map.Add(
                normalizedSourceId,
                normalizedTargetId
            );
        }


        public bool TryResolve(
            string associationId,
            string sourceId,
            out string targetId
        )
        {
            targetId = null;


            if (string.IsNullOrWhiteSpace(
                    associationId
                ) ||
                string.IsNullOrWhiteSpace(
                    sourceId
                ))
            {
                return false;
            }


            if (!associations.TryGetValue(
                    associationId.Trim(),
                    out Dictionary<string, string> map
                ))
            {
                return false;
            }


            return map.TryGetValue(
                sourceId.Trim(),
                out targetId
            );
        }


        public int GetMappingCount(
            string associationId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    associationId
                ))
            {
                return 0;
            }


            return associations.TryGetValue(
                associationId.Trim(),
                out Dictionary<string, string> map
            )
                ? map.Count
                : 0;
        }


        public bool ClearAssociation(
            string associationId
        )
        {
            if (string.IsNullOrWhiteSpace(
                    associationId
                ))
            {
                return false;
            }


            return associations.Remove(
                associationId.Trim()
            );
        }


        public void ClearAll()
        {
            associations.Clear();
        }


        private static string NormalizeRequired(
            string value,
            string parameterName
        )
        {
            if (string.IsNullOrWhiteSpace(
                    value
                ))
            {
                throw new ArgumentException(
                    "Value cannot be null or empty.",
                    parameterName
                );
            }


            return value.Trim();
        }
    }
}
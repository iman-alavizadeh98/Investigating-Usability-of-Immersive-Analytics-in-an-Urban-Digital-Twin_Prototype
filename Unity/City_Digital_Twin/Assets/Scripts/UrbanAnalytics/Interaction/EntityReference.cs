using System;
using UnityEngine;

namespace UrbanAnalytics.Interaction
{
    public enum EntityKind
    {
        SpatialUnit = 0,
        Building = 1
    }


    /// <summary>
    /// Identifies one semantic entity the user can point at,
    /// independent of the meshes that currently draw it.
    ///
    /// A spatial unit is e.g. one Ruta cell (the "block"). A
    /// building also carries the spatial unit it is associated
    /// with, so its block can be resolved without another lookup.
    /// </summary>
    public readonly struct EntityReference :
        IEquatable<EntityReference>
    {
        public EntityKind Kind
        {
            get;
        }


        /// <summary>
        /// Unit ID (e.g. ruta_250:3175006390000) or building ID
        /// (e.g. building:1234).
        /// </summary>
        public string Id
        {
            get;
        }


        /// <summary>
        /// Spatial layer of the unit. For a building: the layer of
        /// its associated unit, or null when it has none.
        /// </summary>
        public string SpatialLayerId
        {
            get;
        }


        /// <summary>
        /// The spatial unit this entity belongs to: the unit
        /// itself, or the building's associated unit (null if
        /// the building is not associated).
        /// </summary>
        public string UnitId
        {
            get;
        }


        public bool IsValid =>
            !string.IsNullOrEmpty(
                Id
            );


        public bool HasUnit =>
            !string.IsNullOrEmpty(UnitId) &&
            !string.IsNullOrEmpty(SpatialLayerId);


        private EntityReference(
            EntityKind kind,
            string id,
            string spatialLayerId,
            string unitId
        )
        {
            Kind =
                kind;

            Id =
                id;

            SpatialLayerId =
                spatialLayerId;

            UnitId =
                unitId;
        }


        public static EntityReference ForUnit(
            string spatialLayerId,
            string unitId
        )
        {
            if (string.IsNullOrWhiteSpace(spatialLayerId) ||
                string.IsNullOrWhiteSpace(unitId))
            {
                throw new ArgumentException(
                    "A spatial-unit reference needs a layer ID " +
                    "and a unit ID."
                );
            }


            return new EntityReference(
                EntityKind.SpatialUnit,
                unitId.Trim(),
                spatialLayerId.Trim(),
                unitId.Trim()
            );
        }


        public static EntityReference ForBuilding(
            string buildingId,
            string associatedUnitId,
            string associatedSpatialLayerId
        )
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                throw new ArgumentException(
                    "A building reference needs a building ID.",
                    nameof(buildingId)
                );
            }


            bool associated =
                !string.IsNullOrWhiteSpace(associatedUnitId) &&
                !string.IsNullOrWhiteSpace(associatedSpatialLayerId);


            return new EntityReference(
                EntityKind.Building,
                buildingId.Trim(),
                associated
                    ? associatedSpatialLayerId.Trim()
                    : null,
                associated
                    ? associatedUnitId.Trim()
                    : null
            );
        }


        /// <summary>
        /// The block (spatial unit) of this entity: itself for a
        /// unit, the associated unit for a building. Returns false
        /// for an unassociated building.
        /// </summary>
        public bool TryGetUnit(
            out EntityReference unit
        )
        {
            unit =
                default;


            if (!IsValid ||
                !HasUnit)
            {
                return false;
            }


            unit =
                Kind == EntityKind.SpatialUnit
                    ? this
                    : ForUnit(
                        SpatialLayerId,
                        UnitId
                    );


            return true;
        }


        /// <summary>
        /// The part of an ID after the layer prefix, e.g.
        /// 3175006390000 for ruta_250:3175006390000.
        /// </summary>
        public static string ShortId(
            string id
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }


            int separator =
                id.LastIndexOf(':');


            return separator >= 0 &&
                   separator < id.Length - 1
                ? id.Substring(separator + 1)
                : id;
        }


        public bool Equals(
            EntityReference other
        )
        {
            return
                Kind == other.Kind &&
                string.Equals(
                    Id,
                    other.Id,
                    StringComparison.Ordinal
                );
        }


        public override bool Equals(
            object obj
        )
        {
            return
                obj is EntityReference other &&
                Equals(other);
        }


        public override int GetHashCode()
        {
            return
                ((int)Kind * 397) ^
                (Id != null
                    ? StringComparer.Ordinal.GetHashCode(Id)
                    : 0);
        }


        public static bool operator ==(
            EntityReference left,
            EntityReference right
        )
        {
            return left.Equals(right);
        }


        public static bool operator !=(
            EntityReference left,
            EntityReference right
        )
        {
            return !left.Equals(right);
        }


        public override string ToString()
        {
            return IsValid
                ? $"{Kind} {Id}"
                : "none";
        }
    }


    /// <summary>
    /// Result of resolving a pointer ray to an entity.
    /// </summary>
    public readonly struct PickResult
    {
        public EntityReference Entity
        {
            get;
        }


        /// <summary>
        /// World-space hit point.
        /// </summary>
        public Vector3 Point
        {
            get;
        }


        /// <summary>
        /// ID of the visualization layer whose geometry was hit
        /// (e.g. a HeightSurface column or a glyph), or null when
        /// the hit was on base city geometry.
        /// </summary>
        public string VisualizationLayerId
        {
            get;
        }


        public bool IsValid =>
            Entity.IsValid;


        public PickResult(
            EntityReference entity,
            Vector3 point,
            string visualizationLayerId
        )
        {
            Entity =
                entity;

            Point =
                point;

            VisualizationLayerId =
                visualizationLayerId;
        }
    }
}

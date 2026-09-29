using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanAnalytics.Visualization
{
    // =============================================================
    // ENUMS
    // =============================================================

    public enum VisualizationMark
    {
        Surface = 0,
        HeightSurface = 1,

        // Reserved for the next renderer implementations.
        BarGlyph = 2,
        StackedBarGlyph = 3,
        RadialGlyph = 4
    }


    public enum VisualizationTargetKind
    {
        SpatialLayer = 0,
        UrbanContextLayer = 1,
        DerivedAnchors = 2
    }


    public enum SpatialMappingMode
    {
        Direct = 0,
        Association = 1
    }


    public enum DataBindingMode
    {
        Single = 0,
        Multiple = 1
    }


    public enum VisualizationChannel
    {
        Color = 0,
        Height = 1,
        Size = 2,
        Opacity = 3,
        Segments = 4,
        Orientation = 5
    }


    public enum VisualizationEncodingRole
    {
        Primary = 0,
        Positive = 1,
        Negative = 2
    }


    public enum ScaleDomainMode
    {
        DataMinMax = 0,
        Manual = 1
    }


    public enum HeightVisualizationMethod
    {
        SurfaceDisplacement = 0,
        FullExtrusion = 1,
        InsetExtrusion = 2,
        DownwardExtrusion = 3
    }


    // =============================================================
    // VISUALIZATION SPEC
    // =============================================================

    /// <summary>
    /// Complete description of one visualization.
    ///
    /// Comparable conceptually to a Figure in a plotting library.
    /// It may contain multiple independent visualization layers.
    /// </summary>
    [Serializable]
    public sealed class VisualizationSpec
    {
        [SerializeField]
        private string id = "visualization";

        [SerializeField]
        private string displayName = "Visualization";

        [SerializeField]
        private List<VisualizationLayerSpec> layers =
            new List<VisualizationLayerSpec>();


        public string Id =>
            id;

        public string DisplayName =>
            displayName;

        public IReadOnlyList<VisualizationLayerSpec> Layers =>
            layers;


        public bool IsConfigured
        {
            get
            {
                if (layers == null ||
                    layers.Count == 0)
                {
                    return false;
                }

                foreach (
                    VisualizationLayerSpec layer
                    in layers
                )
                {
                    if (layer != null &&
                        layer.Enabled &&
                        layer.IsConfigured)
                    {
                        return true;
                    }
                }

                return false;
            }
        }


        public void AddLayer(
            VisualizationLayerSpec layer
        )
        {
            if (layer == null)
            {
                throw new ArgumentNullException(
                    nameof(layer)
                );
            }

            if (layers == null)
            {
                layers =
                    new List<VisualizationLayerSpec>();
            }

            layers.Add(
                layer
            );
        }
    }


    // =============================================================
    // VISUALIZATION LAYER
    // =============================================================

    /// <summary>
    /// One visual representation inside a VisualizationSpec.
    ///
    /// Example:
    ///
    /// Mark = HeightSurface
    /// Target = ruta_250
    /// Color = median_income
    /// Height = population
    /// </summary>
    [Serializable]
    public sealed class VisualizationLayerSpec
    {
        [SerializeField]
        private string id = "layer";

        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private VisualizationMark mark =
            VisualizationMark.Surface;

        [SerializeField]
        private SpatialTargetSpec target =
            new SpatialTargetSpec();

        [SerializeField]
        private List<VisualizationEncodingSpec> encodings =
            new List<VisualizationEncodingSpec>();

        [SerializeField]
        private HeightSurfaceSettings heightSurface =
            new HeightSurfaceSettings();


        public string Id =>
            id;

        public bool Enabled =>
            enabled;

        public VisualizationMark Mark =>
            mark;

        public SpatialTargetSpec Target =>
            target;

        public IReadOnlyList<VisualizationEncodingSpec> Encodings =>
            encodings;

        public HeightSurfaceSettings HeightSurface =>
            heightSurface;


        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(id) &&
            target != null &&
            target.IsConfigured;


        public bool TryGetEncoding(
            VisualizationChannel channel,
            out VisualizationEncodingSpec encoding
        )
        {
            encoding = null;

            if (encodings == null)
            {
                return false;
            }

            foreach (
                VisualizationEncodingSpec candidate
                in encodings
            )
            {
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Channel == channel)
                {
                    encoding = candidate;
                    return true;
                }
            }

            return false;
        }


        public bool TryGetEncoding(
            VisualizationChannel channel,
            VisualizationEncodingRole role,
            out VisualizationEncodingSpec encoding
        )
        {
            encoding = null;

            if (encodings == null)
            {
                return false;
            }

            foreach (
                VisualizationEncodingSpec candidate
                in encodings
            )
            {
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Channel == channel &&
                    candidate.Role == role)
                {
                    encoding = candidate;
                    return true;
                }
            }

            return false;
        }
    }


    // =============================================================
    // SPATIAL TARGET
    // =============================================================

    [Serializable]
    public sealed class SpatialTargetSpec
    {
        [SerializeField]
        private VisualizationTargetKind kind =
            VisualizationTargetKind.SpatialLayer;

        [SerializeField]
        private string layerId = "ruta_250";

        [SerializeField]
        private SpatialMappingSpec mapping =
            new SpatialMappingSpec();


        public VisualizationTargetKind Kind =>
            kind;

        public string LayerId =>
            layerId;

        public SpatialMappingSpec Mapping =>
            mapping;


        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(
                layerId
            );
    }


    [Serializable]
    public sealed class SpatialMappingSpec
    {
        [SerializeField]
        private SpatialMappingMode mode =
            SpatialMappingMode.Direct;

        [SerializeField]
        private string associationId;


        public SpatialMappingMode Mode =>
            mode;

        public string AssociationId =>
            associationId;
    }


    // =============================================================
    // DATA BINDING
    // =============================================================

    [Serializable]
    public sealed class DataVariableReference
    {
        [SerializeField]
        private string dataLayerId;

        [SerializeField]
        private string variableId;


        public string DataLayerId =>
            dataLayerId;

        public string VariableId =>
            variableId;


        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(dataLayerId) &&
            !string.IsNullOrWhiteSpace(variableId);
    }


    [Serializable]
    public sealed class DataBinding
    {
        [SerializeField]
        private DataBindingMode mode =
            DataBindingMode.Single;

        [SerializeField]
        private List<DataVariableReference> variables =
            new List<DataVariableReference>();


        public DataBindingMode Mode =>
            mode;

        public IReadOnlyList<DataVariableReference> Variables =>
            variables;


        public bool TryGetSingle(
            out DataVariableReference variable
        )
        {
            variable = null;

            if (variables == null ||
                variables.Count != 1)
            {
                return false;
            }

            if (variables[0] == null ||
                !variables[0].IsConfigured)
            {
                return false;
            }

            variable =
                variables[0];

            return true;
        }
    }


    // =============================================================
    // ENCODING
    // =============================================================

    /// <summary>
    /// Maps one or more analytical variables to a visual channel.
    /// </summary>
    [Serializable]
    public sealed class VisualizationEncodingSpec
    {
        [SerializeField]
        private VisualizationChannel channel =
            VisualizationChannel.Color;

        [SerializeField]
        private VisualizationEncodingRole role =
            VisualizationEncodingRole.Primary;

        [SerializeField]
        private DataBinding data =
            new DataBinding();

        [SerializeField]
        private NumericScaleSpec scale =
            new NumericScaleSpec();

        [SerializeField]
        private ColorEncodingSettings color =
            new ColorEncodingSettings();

        [SerializeField]
        private HeightEncodingSettings height =
            new HeightEncodingSettings();


        public VisualizationChannel Channel =>
            channel;

        public VisualizationEncodingRole Role =>
            role;

        public DataBinding Data =>
            data;

        public NumericScaleSpec Scale =>
            scale;

        public ColorEncodingSettings Color =>
            color;

        public HeightEncodingSettings Height =>
            height;
    }


    // =============================================================
    // SCALE
    // =============================================================

    [Serializable]
    public sealed class NumericScaleSpec
    {
        [SerializeField]
        private ScaleDomainMode domainMode =
            ScaleDomainMode.DataMinMax;

        [SerializeField]
        private double manualMinimum = 0.0;

        [SerializeField]
        private double manualMaximum = 1.0;


        public ScaleDomainMode DomainMode =>
            domainMode;

        public double ManualMinimum =>
            manualMinimum;

        public double ManualMaximum =>
            manualMaximum;
    }


    // =============================================================
    // COLOR SETTINGS
    // =============================================================

    [Serializable]
    public sealed class ColorEncodingSettings
    {
        [SerializeField]
        private Gradient gradient =
            CreateDefaultGradient();

        [SerializeField]
        private Color noDataColor =
            new Color(
                0.35f,
                0.35f,
                0.35f,
                1.0f
            );

        [SerializeField]
        private bool reverse;


        public Gradient Gradient =>
            gradient;

        public Color NoDataColor =>
            noDataColor;

        public bool Reverse =>
            reverse;


        public Gradient GetGradient()
        {
            if (gradient == null)
            {
                gradient =
                    CreateDefaultGradient();
            }

            return gradient;
        }


        public Color32 Evaluate(
            float normalized
        )
        {
            float t =
                Mathf.Clamp01(
                    normalized
                );

            if (reverse)
            {
                t =
                    1.0f - t;
            }

            return (Color32)GetGradient().Evaluate(
                t
            );
        }


        private static Gradient CreateDefaultGradient()
        {
            var result =
                new Gradient();

            GradientColorKey[] colors =
            {
                new GradientColorKey(
                    new Color32(
                        68,
                        1,
                        84,
                        255
                    ),
                    0.0f
                ),

                new GradientColorKey(
                    new Color32(
                        33,
                        145,
                        140,
                        255
                    ),
                    0.5f
                ),

                new GradientColorKey(
                    new Color32(
                        253,
                        231,
                        37,
                        255
                    ),
                    1.0f
                )
            };

            GradientAlphaKey[] alpha =
            {
                new GradientAlphaKey(
                    1.0f,
                    0.0f
                ),

                new GradientAlphaKey(
                    1.0f,
                    1.0f
                )
            };

            result.SetKeys(
                colors,
                alpha
            );

            return result;
        }
    }


    // =============================================================
    // HEIGHT SETTINGS
    // =============================================================

    [Serializable]
    public sealed class HeightEncodingSettings
    {
        [SerializeField]
        [Min(0.0f)]
        private float maximumVisualHeight =
            0.25f;


        public float MaximumVisualHeight =>
            maximumVisualHeight;
    }


    [Serializable]
    public sealed class HeightSurfaceSettings
    {
        [SerializeField]
        private HeightVisualizationMethod method =
            HeightVisualizationMethod.FullExtrusion;

        [SerializeField]
        [Range(0.1f, 1.0f)]
        private float insetFactor =
            0.8f;


        public HeightVisualizationMethod Method =>
            method;

        public float InsetFactor =>
            insetFactor;
    }
}
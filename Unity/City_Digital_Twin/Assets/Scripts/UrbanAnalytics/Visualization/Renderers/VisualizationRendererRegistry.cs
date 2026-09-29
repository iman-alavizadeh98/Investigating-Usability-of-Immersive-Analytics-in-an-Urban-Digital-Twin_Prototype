using System;
using System.Collections.Generic;

namespace UrbanAnalytics.Visualization
{
    public sealed class VisualizationRendererRegistry
    {
        private readonly Dictionary<
            VisualizationMark,
            IVisualizationRenderer
        > renderers =
            new Dictionary<
                VisualizationMark,
                IVisualizationRenderer
            >();


        public void Register(
            IVisualizationRenderer renderer
        )
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(
                    nameof(renderer)
                );
            }

            renderers[
                renderer.Mark
            ] = renderer;
        }


        public IVisualizationRenderer Get(
            VisualizationMark mark
        )
        {
            if (!renderers.TryGetValue(
                    mark,
                    out IVisualizationRenderer renderer
                ))
            {
                throw new NotSupportedException(
                    $"No visualization renderer is registered " +
                    $"for mark '{mark}'."
                );
            }

            return renderer;
        }


        public bool Supports(
            VisualizationMark mark
        )
        {
            return renderers.ContainsKey(
                mark
            );
        }
    }
}
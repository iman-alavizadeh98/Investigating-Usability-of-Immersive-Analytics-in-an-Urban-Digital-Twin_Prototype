using System;
using System.Collections.Generic;

namespace UrbanAnalytics.Visualization
{
    public sealed class VisualizationRendererRegistry
    {
        private readonly List<
            IVisualizationRenderer
        > renderers =
            new List<
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


            renderers.Add(
                renderer
            );
        }


        public IVisualizationRenderer Get(
            VisualizationLayerSpec spec
        )
        {
            if (spec == null)
            {
                throw new ArgumentNullException(
                    nameof(spec)
                );
            }


            IVisualizationRenderer match =
                null;


            foreach (
                IVisualizationRenderer renderer
                in renderers
            )
            {
                if (!renderer.CanRender(
                        spec
                    ))
                {
                    continue;
                }


                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"More than one visualization renderer " +
                        $"can render layer '{spec.Id}' " +
                        $"(mark={spec.Mark}, " +
                        $"target={spec.Target.Kind})."
                    );
                }


                match =
                    renderer;
            }


            if (match == null)
            {
                throw new NotSupportedException(
                    $"No visualization renderer supports " +
                    $"layer '{spec.Id}' " +
                    $"(mark={spec.Mark}, " +
                    $"target={spec.Target.Kind}, " +
                    $"targetId={spec.Target.LayerId})."
                );
            }


            return match;
        }


        public bool Supports(
            VisualizationLayerSpec spec
        )
        {
            if (spec == null)
            {
                return false;
            }


            foreach (
                IVisualizationRenderer renderer
                in renderers
            )
            {
                if (renderer.CanRender(
                        spec
                    ))
                {
                    return true;
                }
            }


            return false;
        }
    }
}
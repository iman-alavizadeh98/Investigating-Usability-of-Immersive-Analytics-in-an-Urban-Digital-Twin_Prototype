using System.Threading;
using System.Threading.Tasks;

namespace UrbanAnalytics.Visualization
{
    /// <summary>
    /// Renderer plugin for one visualization layer.
    ///
    /// Renderer selection uses the complete layer spec,
    /// not only VisualizationMark.
    /// </summary>
    public interface IVisualizationRenderer
    {
        bool CanRender(
            VisualizationLayerSpec spec
        );


        Task<VisualizationLayerInstance>
            RenderAsync(
                VisualizationRenderContext context,
                VisualizationLayerSpec spec,
                CancellationToken cancellationToken
            );
    }
}
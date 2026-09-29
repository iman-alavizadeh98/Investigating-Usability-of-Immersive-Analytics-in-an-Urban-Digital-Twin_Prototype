using System.Threading;
using System.Threading.Tasks;

namespace UrbanAnalytics.Visualization
{
    public interface IVisualizationRenderer
    {
        VisualizationMark Mark
        {
            get;
        }


        Task<VisualizationLayerInstance> RenderAsync(
            VisualizationRenderContext context,
            VisualizationLayerSpec spec,
            CancellationToken cancellationToken
        );
    }
}
using System.Runtime.CompilerServices;

// The render pipelines' C# sides hand the engine's command buffers, targets and contexts to their native sides by id.
[assembly: InternalsVisibleTo("Helix.RenderPipelines.Core.Runtime")]
[assembly: InternalsVisibleTo("Helix.RenderPipelines.Standard.Runtime")]
[assembly: InternalsVisibleTo("Helix.RenderPipelines.Physical.Runtime")]

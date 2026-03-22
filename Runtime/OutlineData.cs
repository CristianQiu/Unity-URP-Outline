using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// Class to access shared data between different outline subpasses.
/// </summary>
public class OutlineData : ContextItem
{
	#region Public Attributes

	public TextureHandle RenderedObjects { get; set; }

	#endregion

	#region ContextItem Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void Reset()
	{
		RenderedObjects = TextureHandle.nullHandle;
	}

	#endregion
}
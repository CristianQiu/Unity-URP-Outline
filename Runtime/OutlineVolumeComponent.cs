using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume component for the outline.
/// </summary>
[VolumeComponentMenu("Custom/Outline")]
[VolumeRequiresRendererFeatures(typeof(OutlineRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class OutlineVolumeComponent : VolumeComponent, IPostProcessComponent
{
	#region Public Attributes

	public ColorParameter color = new ColorParameter(new Color(0.3f, 0.75f, 1.0f, 1.0f), true, true, true, true);
	public ClampedIntParameter blurRadius = new ClampedIntParameter(5, 2, 32, false);
	public ClampedFloatParameter fallOff = new ClampedFloatParameter(0.015f, 0.0f, 1.0f, false);

	#endregion

	#region Initialization Methods

	public OutlineVolumeComponent() : base()
	{
		displayName = "Outline";
	}

	#endregion

	#region IPostProcessComponent Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsActive()
	{
		return color.value.a > 0.0f;
	}

	#endregion
}
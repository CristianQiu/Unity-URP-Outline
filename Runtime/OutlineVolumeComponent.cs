using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume component for the outline.
/// </summary>
[DisplayInfo(name = "Outline")]
[VolumeComponentMenu("Custom/Outline")]
[VolumeRequiresRendererFeatures(typeof(OutlineRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class OutlineVolumeComponent : VolumeComponent, IPostProcessComponent
{
	#region Public Attributes

	public ClampedIntParameter borderSize = new ClampedIntParameter(0, 0, 16);

	[Header("Colors")]
	public ColorParameter color1 = new ColorParameter(Color.red, true, true, true);
	public ColorParameter color2 = new ColorParameter(Color.green, true, true, true);
	public ColorParameter color3 = new ColorParameter(Color.blue, true, true, true);
	public ColorParameter color4 = new ColorParameter(Color.white, true, true, true);

	[Header("Fill Alphas")]
	public ClampedFloatParameter fillAlpha1 = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
	public ClampedFloatParameter fillAlpha2 = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
	public ClampedFloatParameter fillAlpha3 = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
	public ClampedFloatParameter fillAlpha4 = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

	#endregion

	#region IPostProcessComponent Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsActive()
	{
		bool colorAlphaActive = color1.value.a > 0.0f || color2.value.a > 0.0f || color1.value.a > 0.0f || color1.value.a > 0.0f;
		bool fillAlphaActive = fillAlpha1.value > 0.0f || fillAlpha2.value > 0.0f || fillAlpha3.value > 0.0f || fillAlpha4.value > 0.0f;

		return (borderSize.value > 0 && colorAlphaActive) || fillAlphaActive;
	}

	#endregion
}
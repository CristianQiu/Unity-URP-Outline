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

	public ClampedIntParameter borderSize = new ClampedIntParameter(4, 0, 8);

	[Header("Colors")]
	public ColorParameter color1 = new ColorParameter(new Color(1.0f, 0.0f, 0.0f, 1.0f), true, true, true);
	public ColorParameter color2 = new ColorParameter(new Color(0.0f, 1.0f, 0.0f, 1.0f), true, true, true);
	public ColorParameter color3 = new ColorParameter(new Color(0.0f, 0.0f, 1.0f, 1.0f), true, true, true);
	public ColorParameter color4 = new ColorParameter(new Color(0.0f, 0.0f, 0.0f, 1.0f), true, true, true);

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
		bool active1 = color1.value.a > 0.0f || fillAlpha1.value > 0.0f;
		bool active2 = color2.value.a > 0.0f || fillAlpha2.value > 0.0f;
		bool active3 = color3.value.a > 0.0f || fillAlpha3.value > 0.0f;
		bool active4 = color4.value.a > 0.0f || fillAlpha4.value > 0.0f;

		return borderSize.value > 0 && (active1 || active2 || active3 || active4);
	}

	#endregion
}
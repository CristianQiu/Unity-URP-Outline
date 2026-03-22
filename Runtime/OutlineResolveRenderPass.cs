using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The outline resolve render pass.
/// </summary>
public sealed class OutlineResolveRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the data needed by the execution of the render pass.
	/// </summary>
	private class PassData
	{
		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public Color[] colors;
		public TextureHandle renderedObjects;
	}

	#endregion

	#region Private Attributes

	private const float DefaultRenderScale = 1.0f;
	private const float BorderSizeScalingReferenceHeight = 1440.0f;

	private static readonly int BorderSizeId = Shader.PropertyToID("_BorderSize");
	private static readonly int ColorsId = Shader.PropertyToID("_Colors");
	private static readonly int FillAlphasId = Shader.PropertyToID("_FillAlphas");

	private static readonly int OutlineMaskTextureId = Shader.PropertyToID("_OutlineMaskTexture");

	private Color[] colors = { Color.red, Color.green, Color.blue, Color.black };
	private Material material;

	#endregion

	#region Initialization Methods

	public OutlineResolveRenderPass(Material material) : base()
	{
		profilingSampler = new ProfilingSampler("Outline");
		renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
		requiresIntermediateTexture = false;

		this.material = material;
	}

	#endregion

	#region Scriptable Render Pass Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="frameData"></param>
	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
		OutlineData outlineData = frameData.Get<OutlineData>();

		CreateRenderGraphTextures(renderGraph, resourceData, out TextureHandle resolveTarget);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Resolve", out PassData passData, profilingSampler))
		{
			passData.source = resourceData.activeColorTexture;
			passData.material = material;
			passData.materialPassIndex = 1;
			passData.colors = colors;
			passData.renderedObjects = outlineData.RenderedObjects;

			builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
			builder.UseTexture(outlineData.RenderedObjects);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="resourceData"></param>
	/// <param name="resolveTarget"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, out TextureHandle resolveTarget)
	{
		resolveTarget = renderGraph.CreateTexture(resourceData.cameraColor, "_OutlineResolve");
	}

	/// <summary>
	/// Scales the border to try to keep the same size at different resolutions and/or render scales.
	/// </summary>
	/// <param name="borderSize"></param>
	/// <returns></returns>
	private static float ScaleBorderSizeAccordingToRenderedPixels(float borderSize)
	{
		float renderScale = (UniversalRenderPipeline.asset != null) ? UniversalRenderPipeline.asset.renderScale : DefaultRenderScale;
		float renderHeight = Screen.height * renderScale;

		float borderSizePerHeightUnit = borderSize / BorderSizeScalingReferenceHeight;
		borderSize = borderSizePerHeightUnit * renderHeight;

		borderSize = Mathf.Max(borderSize, 0.0f);
		borderSize = (float)Mathf.RoundToInt(borderSize);

		return borderSize;
	}

	/// <summary>
	/// Updates the material parameters according to the volume settings.
	/// </summary>
	/// <param name="passData"></param>
	private static void UpdateMaterialParameters(PassData passData)
	{
		OutlineVolumeComponent volume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();

		Material material = passData.material;
		Color[] colors = passData.colors;

		colors[0] = volume.color1.value;
		colors[1] = volume.color2.value;
		colors[2] = volume.color3.value;
		colors[3] = volume.color4.value;

		float fillAlpha1 = volume.fillAlpha1.value;
		float fillAlpha2 = volume.fillAlpha2.value;
		float fillAlpha3 = volume.fillAlpha3.value;
		float fillAlpha4 = volume.fillAlpha4.value;

		material.SetFloat(BorderSizeId, ScaleBorderSizeAccordingToRenderedPixels(volume.borderSize.value));
		material.SetColorArray(ColorsId, colors);
		material.SetVector(FillAlphasId, new Vector4(fillAlpha1, fillAlpha2, fillAlpha3, fillAlpha4));
		material.SetTexture(OutlineMaskTextureId, passData.renderedObjects);
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		UpdateMaterialParameters(passData);

		Blitter.BlitTexture(context.cmd, Vector2.one, passData.material, passData.materialPassIndex);
	}

	#endregion
}
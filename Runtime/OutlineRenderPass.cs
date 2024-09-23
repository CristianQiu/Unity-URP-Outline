using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The outline render pass.
/// </summary>
public sealed class OutlineRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// The subpasses the outline render pass is made of.
	/// </summary>
	private enum PassStage : byte
	{
		RenderObjects,
		HorizontalBlur,
		VerticalBlur,
		Resolve,
	}

	/// <summary>
	/// Holds the data needed by the execution of the outline render pass subpasses.
	/// </summary>
	private class PassData
	{
		public PassStage stage;

		public TextureHandle target;
		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public UniversalCameraData cameraData;
		public TextureHandle renderObjectsTarget;
		public RendererListHandle rendererListHandle;

		public TextureHandle blurredRenderObjectsTarget;
		public TextureHandle resolveTarget;
	}

	#endregion

	#region Private Attributes

	private static readonly ShaderTagId[] ShaderTagIds = { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };
	private static readonly uint RenderingLayerMask = UnityEngine.RenderingLayerMask.GetMask("Outline");

	private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
	private static readonly int BlurKernelRadiusId = Shader.PropertyToID("_BlurKernelRadius");
	private static readonly int BlurStandardDeviationId = Shader.PropertyToID("_BlurStandardDeviation");
	private static readonly int OutlineFallOffId = Shader.PropertyToID("_OutlineFallOff");
	private static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");

	private static readonly int RenderObjectsTargetId = Shader.PropertyToID("_OutlineRenderedObjectsMaskTexture");
	private static readonly int BlurredRenderObjectsTargetId = Shader.PropertyToID("_OutlineBlurredRenderedObjectsMaskTexture");

	private Material outlineMaterial;

	#endregion

	#region Initialization Methods

	public OutlineRenderPass(Material outlineMaterial) : base()
	{
		profilingSampler = new ProfilingSampler("Outline");
		renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
		requiresIntermediateTexture = false;

		this.outlineMaterial = outlineMaterial;
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

		CreateRenderGraphTextures(renderGraph, cameraData, out TextureHandle renderObjectsTarget, out TextureHandle horizontalBlurTarget, out TextureHandle verticalBlurTarget, out TextureHandle resolveTarget);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Render Objects Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.RenderObjects;
			passData.cameraData = cameraData;
			passData.renderObjectsTarget = renderObjectsTarget;
			passData.rendererListHandle = CreateRendererList(renderGraph, renderingData, cameraData);

			builder.SetRenderAttachment(renderObjectsTarget, 0);
			builder.UseRendererList(passData.rendererListHandle);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Horizontal Blur Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.HorizontalBlur;
			passData.target = horizontalBlurTarget;
			passData.source = renderObjectsTarget;
			passData.material = outlineMaterial;
			passData.materialPassIndex = 1;

			builder.SetRenderAttachment(passData.target, 0);
			builder.UseTexture(passData.source);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Vertical Blur Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.VerticalBlur;
			passData.target = verticalBlurTarget;
			passData.source = horizontalBlurTarget;
			passData.material = outlineMaterial;
			passData.materialPassIndex = 2;

			builder.SetRenderAttachment(passData.target, 0);
			builder.UseTexture(passData.source);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Resolve Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.Resolve;
			passData.target = resolveTarget;
			passData.source = resourceData.cameraColor;
			passData.material = outlineMaterial;
			passData.materialPassIndex = 3;
			passData.renderObjectsTarget = renderObjectsTarget;
			passData.blurredRenderObjectsTarget = verticalBlurTarget;

			builder.SetRenderAttachment(passData.target, 0);
			builder.UseTexture(passData.source);
			builder.UseTexture(passData.renderObjectsTarget);
			builder.UseTexture(passData.blurredRenderObjectsTarget);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		resourceData.cameraColor = resolveTarget;
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph textures.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="cameraData"></param>
	/// <param name="renderObjectsTarget"></param>
	/// <param name="horizontalBlurTarget"></param>
	/// <param name="verticalBlurTarget"></param>
	/// <param name="resolveTarget"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalCameraData cameraData, out TextureHandle renderObjectsTarget, out TextureHandle horizontalBlurTarget, out TextureHandle verticalBlurTarget, out TextureHandle resolveTarget)
	{
		RenderTextureDescriptor cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
		cameraTargetDescriptor.depthBufferBits = (int)DepthBits.None;
		resolveTarget = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_OutlineResolve", false);

		cameraTargetDescriptor.colorFormat = RenderTextureFormat.R8;
		renderObjectsTarget = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_OutlineRenderObjects", false);
		horizontalBlurTarget = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_OutlineHorizontalBlur", false, FilterMode.Bilinear);
		verticalBlurTarget = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_OutlineVerticalBlur", false, FilterMode.Bilinear);
	}

	/// <summary>
	/// Creates and returns the renderer list used to render the objects in the outline rendering layer.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="renderingData"></param>
	/// <param name="cameraData"></param>
	/// <returns></returns>
	private RendererListHandle CreateRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData)
	{
		RendererListDesc rendererListDesc = new RendererListDesc(ShaderTagIds, renderingData.cullResults, cameraData.camera);

		rendererListDesc.layerMask = ~0;
		rendererListDesc.renderingLayerMask = RenderingLayerMask;
		rendererListDesc.overrideShader = null;
		rendererListDesc.overrideShaderPassIndex = -1;
		rendererListDesc.overrideMaterial = outlineMaterial;
		rendererListDesc.overrideMaterialPassIndex = 0;
		rendererListDesc.renderQueueRange = RenderQueueRange.all;
		rendererListDesc.stateBlock = new RenderStateBlock(RenderStateMask.Nothing);
		rendererListDesc.sortingCriteria = SortingCriteria.None;
		rendererListDesc.rendererConfiguration = PerObjectData.None;
		rendererListDesc.excludeObjectMotionVectors = false;

		return renderGraph.CreateRendererList(rendererListDesc);
	}

	/// <summary>
	/// Updates the material properties that are needed to render the outline.
	/// </summary>
	/// <param name="passData"></param>
	private static void UpdateOutlineMaterialProperties(PassData passData)
	{
		PassStage stage = passData.stage;

		if (stage == PassStage.HorizontalBlur)
		{
			OutlineVolumeComponent outlineVolume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();
			Material outlineMaterial = passData.material;

			outlineMaterial.SetFloat(BlurKernelRadiusId, outlineVolume.blurRadius.value);
			outlineMaterial.SetFloat(BlurStandardDeviationId, Mathf.Floor((float)outlineVolume.blurRadius * 0.5f));
		}
		else if (stage == PassStage.Resolve)
		{
			OutlineVolumeComponent outlineVolume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();
			Material outlineMaterial = passData.material;

			outlineMaterial.SetColor(OutlineColorId, outlineVolume.color.value);
			outlineMaterial.SetFloat(OutlineFallOffId, outlineVolume.fallOff.value);
			outlineMaterial.SetFloat(FillAlphaId, outlineVolume.fillAlpha.value);

			outlineMaterial.SetTexture(RenderObjectsTargetId, passData.renderObjectsTarget);
			outlineMaterial.SetTexture(BlurredRenderObjectsTargetId, passData.blurredRenderObjectsTarget);
		}
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		UpdateOutlineMaterialProperties(passData);

		switch (passData.stage)
		{
			case PassStage.RenderObjects:
				ExecuteRenderObjectsPass(passData, context);
				break;
			default:
				Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
				break;
		}
	}

	/// <summary>
	/// Executes the pass to render the objects that are going to be outlined.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecuteRenderObjectsPass(PassData passData, RasterGraphContext context)
	{
		UniversalCameraData cameraData = passData.cameraData;

		// We need to remove the jitter used by TAA because the rendered outline objects do not
		// write motion vectors and they are not resolved correctly, causing the outline to be jittered.
		bool usingTAA = cameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing;
		Matrix4x4 originalProjectionMatrix = cameraData.GetProjectionMatrix();

		if (usingTAA)
			context.cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), cameraData.camera.nonJitteredProjectionMatrix);

		context.cmd.ClearRenderTarget(RTClearFlags.Color, new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f, 0);
		context.cmd.DrawRendererList(passData.rendererListHandle);

		if (usingTAA)
			context.cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), originalProjectionMatrix);
	}

	#endregion
}
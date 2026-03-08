using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
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

		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public RendererListHandle rendererListHandleR;
		public RendererListHandle rendererListHandleG;
		public RendererListHandle rendererListHandleB;

		public TextureHandle blurredRenderedObjectsColorMaskTarget;
	}

	#endregion

	#region Private Attributes

	private static readonly int BlurKernelRadiusId = Shader.PropertyToID("_BlurKernelRadius");
	private static readonly int BlurStandardDeviationId = Shader.PropertyToID("_BlurStandardDeviation");

	private static readonly Color[] Colors = { Color.red, Color.green, Color.blue };
	private static readonly int MaskColorId = Shader.PropertyToID("_MaskColor");
	private static readonly int ColorsId = Shader.PropertyToID("_Colors");
	private static readonly int FallOffsId = Shader.PropertyToID("_FallOffs");
	private static readonly int FillAlphasId = Shader.PropertyToID("_FillAlphas");

	private static readonly ShaderTagId[] ShaderTagIds = { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };
	private static readonly uint RenderingLayerMaskR = RenderingLayerMask.GetMask("Outline_1");
	private static readonly uint RenderingLayerMaskG = RenderingLayerMask.GetMask("Outline_2");
	private static readonly uint RenderingLayerMaskB = RenderingLayerMask.GetMask("Outline_3");

	private static readonly int BlurredRenderedObjectsTargetId = Shader.PropertyToID("_BlurredRenderedObjectsMaskTexture");

	private Material outlineMaterial;

	#endregion

	#region Initialization Methods

	public OutlineRenderPass(Material outlineMaterial) : base()
	{
		profilingSampler = new ProfilingSampler("Outline");
		renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
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
		UpdateMaterialParameters();

		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		CreateRenderGraphTextures(renderGraph, resourceData, out TextureHandle horizontalBlurTarget, out TextureHandle verticalBlurTarget, out TextureHandle resolveTarget);

		using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("Outline Render Objects Mask", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.RenderObjects;
			passData.rendererListHandleR = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskR, 0);
			passData.rendererListHandleG = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskG, 0);
			passData.rendererListHandleB = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskB, 0);

			builder.SetRenderAttachment(verticalBlurTarget, 0);
			builder.UseRendererList(passData.rendererListHandleR);
			builder.UseRendererList(passData.rendererListHandleG);
			builder.UseRendererList(passData.rendererListHandleB);
			builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafeRenderObjectsPass(data, context));
		}

		RenderGraphUtils.BlitMaterialParameters horizontalBlurBlitParameters = new RenderGraphUtils.BlitMaterialParameters(verticalBlurTarget, horizontalBlurTarget, outlineMaterial, 1);
		RenderGraphUtils.AddBlitPass(renderGraph, horizontalBlurBlitParameters, "Outline Mask Horizontal Blur");

		RenderGraphUtils.BlitMaterialParameters verticalBlurBlitParameters = new RenderGraphUtils.BlitMaterialParameters(horizontalBlurTarget, verticalBlurTarget, outlineMaterial, 2);
		RenderGraphUtils.AddBlitPass(renderGraph, verticalBlurBlitParameters, "Outline Mask Vertical Blur");

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Resolve", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.Resolve;
			passData.source = resourceData.cameraColor;
			passData.material = outlineMaterial;
			passData.materialPassIndex = 3;
			passData.blurredRenderedObjectsColorMaskTarget = verticalBlurTarget;

			builder.SetRenderAttachment(resolveTarget, 0);
			builder.UseTexture(resourceData.cameraColor);
			builder.UseTexture(verticalBlurTarget);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		resourceData.cameraColor = resolveTarget;
	}

	#endregion

	#region Methods

	/// <summary>
	/// Updates the material parameters according to the volume settings.
	/// </summary>
	private void UpdateMaterialParameters()
	{
		OutlineVolumeComponent outlineVolume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();

		outlineMaterial.SetFloat(BlurKernelRadiusId, outlineVolume.blurRadius.value);
		outlineMaterial.SetFloat(BlurStandardDeviationId, Mathf.Floor((float)outlineVolume.blurRadius * 0.5f));

		Colors[0] = outlineVolume.color1.value;
		Colors[1] = outlineVolume.color2.value;
		Colors[2] = outlineVolume.color3.value;

		float fallOff1 = outlineVolume.fallOff1.value;
		float fallOff2 = outlineVolume.fallOff2.value;
		float fallOff3 = outlineVolume.fallOff3.value;

		float fillAlpha1 = outlineVolume.fillAlpha1.value;
		float fillAlpha2 = outlineVolume.fillAlpha2.value;
		float fillAlpha3 = outlineVolume.fillAlpha3.value;

		outlineMaterial.SetColorArray(ColorsId, Colors);
		outlineMaterial.SetVector(FallOffsId, new Vector3(fallOff1, fallOff2, fallOff3));
		outlineMaterial.SetVector(FillAlphasId, new Vector3(fillAlpha1, fillAlpha2, fillAlpha3));
	}

	/// <summary>
	/// Creates and returns the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="resourceData"></param>
	/// <param name="renderedObjectsColorMaskTarget"></param>
	/// <param name="horizontalBlurTarget"></param>
	/// <param name="verticalBlurTarget"></param>
	/// <param name="resolveOutlineTarget"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, out TextureHandle horizontalBlurTarget, out TextureHandle verticalBlurTarget, out TextureHandle resolveOutlineTarget)
	{
		TextureDesc cameraColorDescriptor = renderGraph.GetTextureDesc(resourceData.cameraColor);
		cameraColorDescriptor.clearBuffer = false;
		resolveOutlineTarget = renderGraph.CreateTexture(cameraColorDescriptor);

		cameraColorDescriptor.format = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGB32, false);
		horizontalBlurTarget = renderGraph.CreateTexture(cameraColorDescriptor);
		verticalBlurTarget = renderGraph.CreateTexture(cameraColorDescriptor);
	}

	/// <summary>
	/// Creates and returns the renderer list used to render the objects in the outline rendering layer.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="renderingData"></param>
	/// <param name="cameraData"></param>
	/// <param name="renderingLayerMask"></param>
	/// <param name="passIndex"></param>
	/// <returns></returns>
	private RendererListHandle CreateRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData, uint renderingLayerMask, int passIndex)
	{
		RendererListDesc rendererListDesc = new RendererListDesc(ShaderTagIds, renderingData.cullResults, cameraData.camera);

		rendererListDesc.layerMask = ~0;
		rendererListDesc.renderingLayerMask = renderingLayerMask;
		rendererListDesc.overrideShader = null;
		rendererListDesc.overrideShaderPassIndex = -1;
		rendererListDesc.overrideMaterial = outlineMaterial;
		rendererListDesc.overrideMaterialPassIndex = passIndex;
		rendererListDesc.renderQueueRange = RenderQueueRange.all;
		rendererListDesc.stateBlock = new RenderStateBlock(RenderStateMask.Nothing);
		rendererListDesc.sortingCriteria = SortingCriteria.None;
		rendererListDesc.rendererConfiguration = PerObjectData.None;
		rendererListDesc.excludeObjectMotionVectors = false;

		return renderGraph.CreateRendererList(rendererListDesc);
	}

	/// <summary>
	/// Executes the unsafe pass which renders the 4 layers of objects to apply the outline to.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecuteUnsafeRenderObjectsPass(PassData passData, UnsafeGraphContext context)
	{
		context.cmd.ClearRenderTarget(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));

		context.cmd.SetGlobalColor(MaskColorId, new Color(1.0f, 0.0f, 0.0f, 0x01));
		context.cmd.DrawRendererList(passData.rendererListHandleR);

		context.cmd.SetGlobalColor(MaskColorId, new Color(0.0f, 1.0f, 0.0f, 0x02));
		context.cmd.DrawRendererList(passData.rendererListHandleG);

		context.cmd.SetGlobalColor(MaskColorId, new Color(0.0f, 0.0f, 1.0f, 0x04));
		context.cmd.DrawRendererList(passData.rendererListHandleB);
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		if (passData.stage == PassStage.Resolve)
		{
			Material outlineMaterial = passData.material;
			outlineMaterial.SetTexture(BlurredRenderedObjectsTargetId, passData.blurredRenderedObjectsColorMaskTarget);
			Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
		}
	}

	#endregion
}
using System.Collections.Generic;
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
		ResolveOutline,
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

		public UniversalCameraData cameraData;

		public RendererListHandle rendererListHandleR;
		public RendererListHandle rendererListHandleG;
		public RendererListHandle rendererListHandleB;
		public RendererListHandle rendererListHandleA;

		public TextureHandle renderedObjects;
		public TextureHandle resolveTarget;
	}

	#endregion

	#region Private Attributes

	private static readonly ShaderTagId[] ShaderTagIds = { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") };

	private static readonly uint RenderingLayerMaskR = RenderingLayerMask.GetMask("Outline_1");
	private static readonly uint RenderingLayerMaskG = RenderingLayerMask.GetMask("Outline_2");
	private static readonly uint RenderingLayerMaskB = RenderingLayerMask.GetMask("Outline_3");
	private static readonly uint RenderingLayerMaskA = RenderingLayerMask.GetMask("Outline_4");

	private static readonly int BorderSizeId = Shader.PropertyToID("_BorderSize");

	private static readonly int OutlineColorsId = Shader.PropertyToID("_OutlineColors");
	private static readonly int FillAlphasId = Shader.PropertyToID("_FillAlphas");

	private static readonly int RenderObjectsTargetId = Shader.PropertyToID("_OutlineRenderedObjectsMaskTexture");

	private static readonly List<Color> ColorsList = new List<Color>(4);

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
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		CreateRenderGraphTextures(renderGraph, cameraData, out TextureHandle renderedObjects, out TextureHandle resolveTarget);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Render Objects Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.RenderObjects;
			passData.cameraData = cameraData;
			passData.renderedObjects = renderedObjects;
			passData.rendererListHandleR = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskR, 1);
			passData.rendererListHandleG = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskG, 2);
			passData.rendererListHandleB = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskB, 3);
			passData.rendererListHandleA = CreateRendererList(renderGraph, renderingData, cameraData, RenderingLayerMaskA, 4);
			passData.material = outlineMaterial;

			builder.SetRenderAttachment(renderedObjects, 0);
			builder.UseRendererList(passData.rendererListHandleR);
			builder.UseRendererList(passData.rendererListHandleG);
			builder.UseRendererList(passData.rendererListHandleB);
			builder.UseRendererList(passData.rendererListHandleA);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Outline Resolve Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.ResolveOutline;
			passData.source = resourceData.cameraColor;
			passData.material = outlineMaterial;
			passData.materialPassIndex = 5;
			passData.renderedObjects = renderedObjects;

			builder.SetRenderAttachment(resolveTarget, 0);
			builder.UseTexture(passData.source);
			builder.UseTexture(passData.renderedObjects);
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
	/// <param name="renderedObjects"></param>
	/// <param name="horizontalBlurTarget"></param>
	/// <param name="verticalBlurTarget"></param>
	/// <param name="resolveTarget"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalCameraData cameraData, out TextureHandle renderedObjects, out TextureHandle resolveTarget)
	{
		RenderTextureDescriptor cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
		cameraTargetDescriptor.depthBufferBits = (int)DepthBits.None;
		resolveTarget = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_OutlineResolve", false);

		// TODO :optimize
		cameraTargetDescriptor.colorFormat = RenderTextureFormat.ARGB4444;
		renderedObjects = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_OutlineCombinedMask", false, FilterMode.Point);
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
		RenderStateBlock block = new RenderStateBlock(RenderStateMask.Depth);
		block.depthState = new DepthState(false, CompareFunction.Always);
		//rendererListDesc.stateBlock = new RenderStateBlock(RenderStateMask.Nothing);
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
		OutlineVolumeComponent outlineVolume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();
		Material outlineMaterial = passData.material;

		if (stage == PassStage.RenderObjects)
		{
			// TODO: compensate border for different res
			outlineMaterial.SetFloat(BorderSizeId, outlineVolume.borderSize.value);
		}
		else if (stage == PassStage.ResolveOutline)
		{
			while (ColorsList.Count < 4)
				ColorsList.Add(new Color(1.0f, 1.0f, 1.0f, 1.0f));

			ColorsList[0] = outlineVolume.color1.value;
			ColorsList[1] = outlineVolume.color2.value;
			ColorsList[2] = outlineVolume.color3.value;
			ColorsList[3] = outlineVolume.color4.value;

			outlineMaterial.SetColorArray(OutlineColorsId, ColorsList);

			Vector4 fillAlphas = new Vector4(outlineVolume.fillAlpha1.value, outlineVolume.fillAlpha2.value, outlineVolume.fillAlpha3.value, outlineVolume.fillAlpha4.value);
			outlineMaterial.SetVector(FillAlphasId, fillAlphas);

			outlineMaterial.SetTexture(RenderObjectsTargetId, passData.renderedObjects);
		}
	}

	private float ModifyBorderSizeAccordingToResolutionWindowAndRenderScale(float borderSize)
	{
		// try to look for the render scale in the asset or default to 1 if not found
		float renderScale = 1.0f;
		if (UniversalRenderPipeline.asset != null)
			renderScale = UniversalRenderPipeline.asset.renderScale;

		// use the height as the reference, as it usually constrains more than width (height is
		// lesser than width on most screens)
		float currRenderHeight = Screen.height * renderScale;

		// use 1440 as the reference height, as it is usually a good point between 4k and 720p, but
		// 1080p could also be used
		const float ReferenceHeight = 1440.0f;

		float borderSizePerHeightUnit = borderSize / ReferenceHeight;
		float newBorderSize = borderSizePerHeightUnit * currRenderHeight;

		// dont let it be less than 1 (represents a one texel shift in the shader)
		newBorderSize = Mathf.Max(newBorderSize, 1.0f);

		// round it to sample texels at their center
		newBorderSize = (float)Mathf.RoundToInt(newBorderSize);

		return newBorderSize;
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
				context.cmd.DrawRendererList(passData.rendererListHandleR);
				context.cmd.DrawRendererList(passData.rendererListHandleG);
				context.cmd.DrawRendererList(passData.rendererListHandleB);
				context.cmd.DrawRendererList(passData.rendererListHandleA);
				break;
			default:
				Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
				break;
		}
	}

	#endregion
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The outline renderer feature.
/// </summary>
[Tooltip("Adds support to render an outline with optional fill for objects.")]
[DisallowMultipleRendererFeature("Outline")]
public sealed class OutlineRendererFeature : ScriptableRendererFeature
{
	#region Private Attributes

	[HideInInspector]
	[SerializeField] private Shader shader;

	private Material material;

	private OutlineDrawMaskRenderPass drawMaskRenderPass;
	private OutlineResolveRenderPass resolveRenderPass;

	#endregion

	#region Scriptable Renderer Feature Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void Create()
	{
		ValidateResources(true);

		drawMaskRenderPass = new OutlineDrawMaskRenderPass(material);
		resolveRenderPass = new OutlineResolveRenderPass(material);
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderer"></param>
	/// <param name="renderingData"></param>
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		bool isPostProcessEnabled = renderingData.postProcessingEnabled && renderingData.cameraData.postProcessEnabled;
		bool shouldAddRenderPasses = isPostProcessEnabled && ShouldAddRenderPasses(renderingData.cameraData.cameraType);

		if (shouldAddRenderPasses)
		{
			renderer.EnqueuePass(drawMaskRenderPass);
			renderer.EnqueuePass(resolveRenderPass);
		}
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="disposing"></param>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		CoreUtils.Destroy(material);
	}

	#endregion

	#region Methods

	/// <summary>
	/// Validates the resources used by the render passes.
	/// </summary>
	/// <param name="forceRefresh"></param>
	/// <returns></returns>
	private bool ValidateResources(bool forceRefresh)
	{
		if (forceRefresh)
		{
#if UNITY_EDITOR
			shader = Shader.Find("Hidden/Outline");
#endif
			CoreUtils.Destroy(material);
			material = CoreUtils.CreateEngineMaterial(shader);
		}

		return shader != null && material != null;
	}

	/// <summary>
	/// Gets whether the render passes should be enqueued to the renderer.
	/// </summary>
	/// <param name="cameraType"></param>
	/// <returns></returns>
	private bool ShouldAddRenderPasses(CameraType cameraType)
	{
		OutlineVolumeComponent volume = VolumeManager.instance.stack.GetComponent<OutlineVolumeComponent>();

		bool isVolumeOk = volume != null && volume.IsActive();
		bool areRenderPassesOk = drawMaskRenderPass != null && resolveRenderPass != null;
		bool areResourcesOk = ValidateResources(false);
		bool isCameraOk = cameraType != CameraType.Preview && cameraType != CameraType.Reflection;

		return isActive && isVolumeOk && areRenderPassesOk && areResourcesOk && isCameraOk;
	}

	#endregion
}
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.UI;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	public class PopupCameraConfetti : MonoBehaviour
	{
		[SerializeField] private ParticleSystem _leftConfetti;
		[SerializeField] private ParticleSystem _rightConfetti;
		[SerializeField] private Mesh[] _fishMeshes;
		[SerializeField] private Material[] _fishMaterials;
		[SerializeField] private Mesh[] _treasureMeshes;
		[SerializeField] private Material[] _treasureMaterials;
		[SerializeField] private Vector3 _leftViewportAnchor = new Vector3(0.22f, 0.82f, 8f);
		[SerializeField] private Vector3 _rightViewportAnchor = new Vector3(0.78f, 0.82f, 8f);
		[SerializeField] private Vector3 _leftRotationOffset = new Vector3(-45f, 45f, -99.309f);
		[SerializeField] private Vector3 _rightRotationOffset = new Vector3(-45f, -45f, -99.309f);

		private const string FishBurstNamePrefix = "Wishlist Fish Loot Burst";
		private const string TreasureBurstNamePrefix = "Wishlist Treasure Loot Burst";
		private Popup _popup;

		private void Awake()
		{
			_popup = GetComponent<Popup>();
			ConfigureLootBurst(_leftConfetti);
			ConfigureLootBurst(_rightConfetti);
		}

		private void OnEnable()
		{
			if (_popup == null)
				_popup = GetComponent<Popup>();

			if (_popup == null)
				return;

			_popup.OnOpened += HandlePopupOpened;
			_popup.OnClosed += HandlePopupClosed;
		}

		private void OnDisable()
		{
			if (_popup == null)
				return;

			_popup.OnOpened -= HandlePopupOpened;
			_popup.OnClosed -= HandlePopupClosed;
		}

		private void LateUpdate()
		{
			if (_popup == null || !_popup.IsOpen)
				return;

			PositionConfetti();
		}

		private void HandlePopupOpened(Popup popup)
		{
			ConfigureLootBurst(_leftConfetti);
			ConfigureLootBurst(_rightConfetti);
			PositionConfetti();
			Restart(_leftConfetti);
			Restart(_rightConfetti);
		}

		private void HandlePopupClosed(Popup popup)
		{
			Stop(_leftConfetti);
			Stop(_rightConfetti);
		}

		[Button]
		private void PositionConfetti()
		{
			var cameraToUse = Camera.main;
			if (cameraToUse == null && CoreGameManager.Instance != null)
				cameraToUse = CoreGameManager.Instance.MainCamera;

			if (cameraToUse == null)
				return;

			PositionParticleSystem(_leftConfetti, cameraToUse, _leftViewportAnchor, _leftRotationOffset);
			PositionParticleSystem(_rightConfetti, cameraToUse, _rightViewportAnchor, _rightRotationOffset);
		}

		private static void PositionParticleSystem(ParticleSystem particleSystem, Camera cameraToUse, Vector3 viewportAnchor, Vector3 rotationOffset)
		{
			if (particleSystem == null)
				return;

			var particleTransform = particleSystem.transform;
			var worldPosition = cameraToUse.ViewportToWorldPoint(viewportAnchor);
			var worldRotation = cameraToUse.transform.rotation * Quaternion.Euler(rotationOffset);

			particleTransform.SetPositionAndRotation(worldPosition, worldRotation);
		}

		private void ConfigureLootBurst(ParticleSystem fishBurst)
		{
			if (fishBurst == null)
				return;

			ConfigureLootVariants(fishBurst, FishBurstNamePrefix, _fishMeshes, _fishMaterials, useParentForFirstVariant: true);
			ConfigureLootVariants(fishBurst, TreasureBurstNamePrefix, _treasureMeshes, _treasureMaterials, useParentForFirstVariant: false);
		}

		private static void ConfigureLootVariants(
			ParticleSystem rootBurst,
			string childNamePrefix,
			Mesh[] meshes,
			Material[] materials,
			bool useParentForFirstVariant)
		{
			if (rootBurst == null || meshes == null || materials == null)
				return;

			var variantCount = Mathf.Min(meshes.Length, materials.Length);
			for (var i = 0; i < variantCount; i++)
			{
				if (meshes[i] == null || materials[i] == null)
					continue;

				var variantBurst = useParentForFirstVariant && i == 0
					? rootBurst
					: GetOrCreateVariantBurst(rootBurst, childNamePrefix, i);
				ConfigureMeshParticleRenderer(variantBurst, meshes[i], materials[i]);
			}
		}

		private static ParticleSystem GetOrCreateVariantBurst(ParticleSystem rootBurst, string childNamePrefix, int variantIndex)
		{
			var childName = $"{childNamePrefix} {variantIndex + 1}";
			var existing = rootBurst.transform.Find(childName);
			if (existing != null && existing.TryGetComponent<ParticleSystem>(out var existingParticleSystem))
				return existingParticleSystem;

			var variantBurstObject = Instantiate(rootBurst.gameObject);
			variantBurstObject.name = childName;
			RemoveNestedLootBursts(variantBurstObject.transform);
			var variantBurstTransform = variantBurstObject.transform;
			variantBurstTransform.SetParent(rootBurst.transform, false);
			variantBurstTransform.localPosition = Vector3.zero;
			variantBurstTransform.localRotation = Quaternion.identity;
			variantBurstTransform.localScale = Vector3.one;

			return variantBurstObject.GetComponent<ParticleSystem>();
		}

		private static void RemoveNestedLootBursts(Transform root)
		{
			for (var i = root.childCount - 1; i >= 0; i--)
			{
				var child = root.GetChild(i);
				if (!child.name.StartsWith(FishBurstNamePrefix, System.StringComparison.Ordinal) &&
				    !child.name.StartsWith(TreasureBurstNamePrefix, System.StringComparison.Ordinal))
					continue;

				Destroy(child.gameObject);
			}
		}

		private static void ConfigureMeshParticleRenderer(ParticleSystem particleSystem, Mesh mesh, Material material)
		{
			if (particleSystem == null)
				return;

			var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
			if (particleRenderer == null)
				return;

			if (mesh != null)
			{
				particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
				particleRenderer.mesh = mesh;
				particleRenderer.SetMeshes(new[] { mesh }, 1);
				particleRenderer.enableGPUInstancing = false;
			}

			if (material != null)
				particleRenderer.sharedMaterials = new[] { material };
		}

		private static void Restart(ParticleSystem particleSystem)
		{
			if (particleSystem == null)
				return;

			particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			particleSystem.Play(true);
		}

		private static void Stop(ParticleSystem particleSystem)
		{
			if (particleSystem == null)
				return;

			particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
	}
}

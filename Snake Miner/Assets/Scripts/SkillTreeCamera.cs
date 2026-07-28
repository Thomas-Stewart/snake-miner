using System;
using SSG_Core.Scripts.Input;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class SkillTreeCamera : MonoBehaviour
{
	[SerializeField] private float _panSpeed;
	[SerializeField] private float _zoomSpeed;
	[SerializeField] private float _focusLerpSpeed = 8f;
	[SerializeField] private float _maxRestoredOrthographicSize = 300f;

	private static Vector3? _lastPosition;
	private static float? _lastOrthographicSize;
	private Camera _camera;
	private Vector3? _focusTargetPosition;

	private void Awake()
	{
		_camera = GetComponent<Camera>();
		if (_lastPosition.HasValue)
			transform.position = _lastPosition.Value;
		if (_lastOrthographicSize.HasValue)
			_camera.orthographicSize = Mathf.Min(_lastOrthographicSize.Value, _maxRestoredOrthographicSize);
	}

	private void OnDisable()
	{
		if (_camera == null)
			return;

		_lastPosition = transform.position;
		_lastOrthographicSize = _camera.orthographicSize;
	}

	public static void ResetCachedState()
	{
		_lastPosition = null;
		_lastOrthographicSize = null;
	}

	private void Update()
	{
		var moveInput = InputManager.InputActions.SkillTree.PanSkillTree.ReadValue<Vector2>();
		var pos = transform.position;
		pos += new Vector3(moveInput.x, moveInput.y, 0f) * (_panSpeed * Time.deltaTime);
		transform.position = pos;
		if (moveInput.sqrMagnitude > 0.001f)
			_focusTargetPosition = transform.position;

		var zoomInput = -InputManager.InputActions.SkillTree.ZoomSkillTree.ReadValue<float>();
		var orthoSize = _camera.orthographicSize;
		orthoSize = Mathf.Max(100, orthoSize + zoomInput * _zoomSpeed);
		_camera.orthographicSize = orthoSize;

		ClickDrag();
		UpdateFocusLerp();
	}

	public void FocusOn(Vector3 worldPosition)
	{
		var pos = transform.position;
		pos.x = worldPosition.x;
		pos.y = worldPosition.y;
		_focusTargetPosition = pos;
	}

	public void CenterOn(Vector3 worldPosition)
	{
		var pos = transform.position;
		pos.x = worldPosition.x;
		pos.y = worldPosition.y;
		transform.position = pos;
		_focusTargetPosition = pos;
	}

	private void ClickDrag()
	{
		if (_camera == null)
			return;

		var mouse = Mouse.current;
		if (mouse == null)
			return;

		var mousePosition = mouse.position.ReadValue();
		if (mouse.leftButton.wasPressedThisFrame)
		{
			_dragOrigin = _camera.ScreenToWorldPoint(mousePosition);
		}

		if (mouse.leftButton.isPressed)
		{
			var current = _camera.ScreenToWorldPoint(mousePosition);
			var delta = _dragOrigin - current;

			transform.position += new Vector3(delta.x, delta.y, 0f);
			_focusTargetPosition = transform.position;
		}
	}

	private void UpdateFocusLerp()
	{
		if (!_focusTargetPosition.HasValue)
			return;

		var target = _focusTargetPosition.Value;
		var current = transform.position;
		var t = 1f - Mathf.Exp(-_focusLerpSpeed * Time.unscaledDeltaTime);
		current.x = Mathf.Lerp(current.x, target.x, t);
		current.y = Mathf.Lerp(current.y, target.y, t);
		transform.position = current;
	}
	private Vector3 _dragOrigin;
}

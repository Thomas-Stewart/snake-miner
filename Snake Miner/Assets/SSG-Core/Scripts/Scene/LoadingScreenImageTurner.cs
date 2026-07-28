using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Util;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SSG_Core.Scripts.Scene
{
	public class LoadingScreenImageTurner : MonoBehaviour
	{
		[SerializeField] private Image[] _images;
		[SerializeField] private MinMaxValue _turnTime;

		[Header("Wave Settings")]
		[SerializeField] private bool _shouldUseWave;
		[SerializeField] private float _timeBetweenTurns = 0.2f;
		[SerializeField] private float _delayBeforeWaveStart = 0.5f;
		[SerializeField] private float _timeBetweenWaves = 1.5f;

		[Header("Random Settings")]
		[SerializeField] private float _quantityFrequency = 1f;
		[SerializeField] private int _quantityPerUpdate = 2;
		[SerializeField] private AnimationCurve _curve;

		private float _timeLastTurned;
		private int _numInColumn;

		private void Awake()
		{
			_numInColumn = GetComponent<GridLayoutGroup>().constraintCount;
		}

		[Button]
		private void LinkImages()
		{
			_images = GetComponentsInChildren<Image>();
		}

		private void OnDisable()
		{
			_timeLastTurned = -1;
			_indicesInUse.Clear();
			foreach (var image in _images)
			{
				image.transform.eulerAngles = Vector3.zero;
			}
		}

		private void Update()
		{
			if (!CoreGameManager.Instance.IsLoadingScreenShowing) return;

			if (_shouldUseWave)
			{
				if (_timeLastTurned <= 0 || Time.time - _timeLastTurned > _timeBetweenWaves)
				{
					_timeLastTurned = Time.time;
					StartCoroutine(WaveRoutine());
				}
			}
			else
			{
				if (Time.time - _timeLastTurned < _quantityFrequency) return;

				_timeLastTurned = Time.time;
				for (var i = 0; i < _quantityPerUpdate; i++)
				{
					var randIndex = GetRandIndex();
					if (!_indicesInUse.Contains(randIndex))
						StartCoroutine(TurnImageRoutine(randIndex));
				}
			}
		}

		private IEnumerator WaveRoutine()
		{
			yield return new WaitForSeconds(_delayBeforeWaveStart);
			var numRows = (int)Math.Ceiling((double)_images.Length / _numInColumn);
			for (var sum = 0; sum <= numRows + _numInColumn - 2; ++sum)
			{
				yield return new WaitForSeconds(_timeBetweenTurns);
				for (var row = numRows - 1; row >= 0; --row)
				{
					var col = sum - row;
					if (col >= 0 && col < _numInColumn && row * _numInColumn + col < _images.Length)
					{
						var itemIndex = row * _numInColumn + col;
						if (!_indicesInUse.Contains(itemIndex))
						{
							StartCoroutine(TurnImageRoutine(itemIndex));
						}
					}
				}
			}
		}

		private List<int> _indicesInUse = new();
		private int GetRandIndex()
		{
			var safety = 0;
			int i;
			do
			{
				i = Random.Range(0, _images.Length);
				safety++;
			} while (_indicesInUse.Contains(i) && safety < 100);

			return i;
		}

		private IEnumerator TurnImageRoutine(int index)
		{
			_indicesInUse.Add(index);

			var image = _images[index];
			var transform1 = image.transform;
			var timer = 0f;
			var step = 0f;
			var curAngles = transform1.eulerAngles;

			var startRot = curAngles.z;

			var targetRot = curAngles.z + 90;
			if (Random.Range(0, 2) == 1 && ! _shouldUseWave)
				targetRot -= 180;

			var turnTime = _shouldUseWave ? _turnTime.Max : _turnTime.GetRandValue();

			var safety = 0;
			while (step < 1.0f && safety < 1000)
			{
				safety++;
				step = _curve.Evaluate(timer / turnTime);
				var rotation = Mathf.Lerp(startRot, targetRot, step);
				curAngles.z = rotation;
				transform1.eulerAngles = curAngles;
				timer += Time.deltaTime;
				yield return null;
			}
			curAngles.z = targetRot;
			transform1.eulerAngles = curAngles;

			_indicesInUse.Remove(index);
		}

	}
}
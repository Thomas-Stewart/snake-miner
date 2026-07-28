using System;

namespace SSG_Core.Scripts.Util
{
	public class FixedSizeStack<T>
	{
		private readonly T[] _array;
		private int _index;

		public FixedSizeStack(int capacity)
		{
			_array = new T[capacity];
			_index = 0;
		}

		public void Push(T item)
		{
			_array[_index] = item;
			_index = (_index + 1) % _array.Length; // Update index in a circular manner
		}

		public void Clear()
		{
			Array.Clear(_array, 0, _array.Length);
			_index = 0;
		}

		public T Pop()
		{
			if (_index == 0)
			{
				_index = _array.Length - 1;
			}
			else
			{
				_index--;
			}

			return _array[_index];
		}

		public T Peek()
		{
			int peekIndex = _index == 0 ? _array.Length - 1 : _index - 1;
			return _array[peekIndex];
		}

		public int Count => Math.Min(_array.Length, _index);

		// Other methods or properties you might need
	}
}
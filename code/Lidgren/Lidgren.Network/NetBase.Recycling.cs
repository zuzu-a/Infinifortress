/* ----------------------------------------------------------------------------
MIT License

Copyright (c) 2008 Michael Lidgren
Copyright (c) 2023 Christopher Whitley

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
---------------------------------------------------------------------------- */

using System.Text;

namespace Lidgren.Network;

public partial class NetBase
{
	private const int c_smallBufferSize = 24;
	private const int c_maxSmallItems = 32;
	private const int c_maxLargeItems = 16;

	private Stack<NetBuffer> m_smallBufferPool = new Stack<NetBuffer>(c_maxSmallItems);
	private Stack<NetBuffer> m_largeBufferPool = new Stack<NetBuffer>(c_maxLargeItems);
	private object m_smallBufferPoolLock = new object();
	private object m_largeBufferPoolLock = new object();

	internal void RecycleBuffer(NetBuffer item)
	{
		if (!m_config.m_useBufferRecycling)
			return;

		if (item.Data.Length <= c_smallBufferSize)
		{
			lock (m_smallBufferPoolLock)
			{
				if (m_smallBufferPool.Count >= c_maxSmallItems)
					return; // drop, we're full
				m_smallBufferPool.Push(item);
			}
			return;
		}
		lock (m_largeBufferPoolLock)
		{
			if (m_largeBufferPool.Count >= c_maxLargeItems)
				return; // drop, we're full
			m_largeBufferPool.Push(item);
		}
		return;
	}

	public NetBuffer CreateBuffer(int initialCapacity)
	{
		if (m_config.m_useBufferRecycling)
		{
			NetBuffer retval;
			if (initialCapacity <= c_smallBufferSize)
			{
				lock (m_smallBufferPoolLock)
				{
					if (m_smallBufferPool.Count == 0)
						return new NetBuffer(initialCapacity);
					retval = m_smallBufferPool.Pop();
				}
				retval.Reset();
				return retval;
			}

			lock (m_largeBufferPoolLock)
			{
				if (m_largeBufferPool.Count == 0)
					return new NetBuffer(initialCapacity);
				retval = m_largeBufferPool.Pop();
			}
			retval.Reset();
			return retval;
		}
		else
		{
			return new NetBuffer(initialCapacity);
		}
	}

	public NetBuffer CreateBuffer(string str)
	{
		// TODO: optimize
		NetBuffer retval = CreateBuffer(Encoding.UTF8.GetByteCount(str) + 1);
		retval.Write(str);
		return retval;
	}

	public NetBuffer CreateBuffer()
	{
		return CreateBuffer(m_config.m_defaultBufferCapacity);
	}

}

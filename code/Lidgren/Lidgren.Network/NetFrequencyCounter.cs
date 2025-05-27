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

namespace Lidgren.Network;

public sealed class NetFrequencyCounter
{
	private float m_windowSize;
	private double m_windowEnd;
	private int m_windowCount;
	private double m_lastCount;
	private double m_countLow, m_countHigh;

	private float m_frequency;
	private float m_low, m_high;

	public float AverageFrequency { get { return m_frequency; } }
	public float LowestFrequency { get { return m_low; } }
	public float HighestFrequency { get { return m_high; } }

	public NetFrequencyCounter(float windowSizeSeconds)
	{
		m_windowSize = windowSizeSeconds;
		m_windowEnd = NetTime.Now + m_windowSize;
		m_countLow = float.MinValue;
		m_countHigh = float.MaxValue;
		m_lastCount = 0;
	}

	public void Count()
	{
		Count(NetTime.Now);
	}

	public void Count(double now)
	{
		double thisLength = now - m_lastCount;
		if (thisLength > m_countLow)
			m_countLow = thisLength;
		if (thisLength < m_countHigh)
			m_countHigh = thisLength;

		if (now > m_windowEnd)
		{
			m_frequency = (float)((double)m_windowCount / (m_windowSize + (now - m_windowEnd)));
			m_low = (float)(1.0 / m_countLow);
			m_high = (float)(1.0 / m_countHigh);
			m_countLow = float.MinValue;
			m_countHigh = float.MaxValue;

			m_windowEnd += m_windowSize;
			m_windowCount = 0;
		}
		m_windowCount++;
		m_lastCount = now;
	}
}

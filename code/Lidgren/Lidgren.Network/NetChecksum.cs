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

/// <summary>
/// CCITT-16 and Adler16
/// </summary>
public static class NetChecksum
{
	private static ushort[] m_table;

	static NetChecksum()
	{
		m_table = new ushort[256];

		// generate lookup table for ccitt16
		for (int i = 0; i < 256; i++)
		{
			ushort crc = (ushort)i;
			crc <<= 8;
			for (int j = 0; j < 8; j++)
			{
				ushort bit = (ushort)(crc & 32768);
				crc <<= 1;
				if (bit != 0)
					crc ^= 0x1021;
			}
			m_table[i] = crc;
		}
	}

	[CLSCompliant(false)]
	public static ushort CalculateCCITT16(byte[] data, int offset, int len)
	{
		ulong crc = 0x1D0F;
		for (int i = 0; i < len; i++)
			crc = (crc << 8) ^ m_table[((crc >> 8) & 0xff) ^ data[offset + i]];
		return (ushort)crc;
	}

	// Adler16; superior to adler32 and fletcher16 for small size data
	// see http://www.zlib.net/maxino06_fletcher-adler.pdf
	[CLSCompliant(false)]
	public static ushort Adler16(byte[] data, int offset, int len)
	{
		int a = 1;
		int b = 0;

		int ptr = offset;
		int end = offset + len;
		while (ptr < end)
		{
			int tlen = (end - ptr > 5550 ? 5550 : end - ptr);
			for (int i = 0; i < tlen; i++)
			{
				a += data[ptr++];
				b += a;
			}
			a %= 251;
			b %= 251;
		}
		return (ushort)(b << 8 | a);
	}
}

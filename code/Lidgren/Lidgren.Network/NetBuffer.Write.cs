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

using System.Diagnostics;
using System.Text;
using System.Net;

namespace Lidgren.Network;

public sealed partial class NetBuffer
{
	//
	// 1 bit
	//
	public void Write(bool value)
	{
		InternalEnsureBufferSize(m_bitLength + 1);
		NetBitWriter.WriteByte((value ? (byte)1 : (byte)0), 1, Data, m_bitLength);
		m_bitLength += 1;
	}

	//
	// 8 bit
	//
	public void Write(byte source)
	{
		InternalEnsureBufferSize(m_bitLength + 8);
		NetBitWriter.WriteByte(source, 8, Data, m_bitLength);
		m_bitLength += 8;
	}

	[CLSCompliant(false)]
	public void Write(sbyte source)
	{
		InternalEnsureBufferSize(m_bitLength + 8);
		NetBitWriter.WriteByte((byte)source, 8, Data, m_bitLength);
		m_bitLength += 8;
	}

	public void Write(byte source, int numberOfBits)
	{
		Debug.Assert((numberOfBits > 0 && numberOfBits <= 8), "Write(byte, numberOfBits) can only write between 1 and 8 bits");
		InternalEnsureBufferSize(m_bitLength + numberOfBits);
		NetBitWriter.WriteByte(source, numberOfBits, Data, m_bitLength);
		m_bitLength += numberOfBits;
	}

	public void Write(byte[] source)
	{
		if (source == null)
			throw new ArgumentNullException("source");
		int bits = source.Length * 8;
		InternalEnsureBufferSize(m_bitLength + bits);
		NetBitWriter.WriteBytes(source, 0, source.Length, Data, m_bitLength);
		m_bitLength += bits;
	}

	public void Write(byte[] source, int offsetInBytes, int numberOfBytes)
	{
		if (source == null)
			throw new ArgumentNullException("source");
		int bits = numberOfBytes * 8;
		InternalEnsureBufferSize(m_bitLength + bits);
		NetBitWriter.WriteBytes(source, offsetInBytes, numberOfBytes, Data, m_bitLength);
		m_bitLength += bits;
	}

	//
	// 16 bit
	//
	[CLSCompliant(false)]
	public void Write(UInt16 source)
	{
		InternalEnsureBufferSize(m_bitLength + 16);
		NetBitWriter.WriteUInt32((uint)source, 16, Data, m_bitLength);
		m_bitLength += 16;
	}

	[CLSCompliant(false)]
	public void Write(UInt16 source, int numberOfBits)
	{
		Debug.Assert((numberOfBits > 0 && numberOfBits <= 16), "Write(ushort, numberOfBits) can only write between 1 and 16 bits");
		InternalEnsureBufferSize(m_bitLength + numberOfBits);
		NetBitWriter.WriteUInt32((uint)source, numberOfBits, Data, m_bitLength);
		m_bitLength += numberOfBits;
	}

	[CLSCompliant(false)]
	public void Write(Int16 source)
	{
		InternalEnsureBufferSize(m_bitLength + 16);
		NetBitWriter.WriteUInt32((uint)source, 16, Data, m_bitLength);
		m_bitLength += 16;
	}

	//
	// 32 bit
	//
#if UNSAFE
		public unsafe void Write(Int32 source)
		{
			InternalEnsureBufferSize(m_bitLength + 32);

			// can write fast?
			if (m_bitLength % 8 == 0)
			{
				fixed (byte* numRef = &Data[m_bitLength / 8])
				{
					*((int*)numRef) = source;
				}
			}
			else
			{
				NetBitWriter.WriteUInt32((UInt32)source, 32, Data, m_bitLength);
			}
			m_bitLength += 32;
		}
#else
	public void Write(Int32 source)
	{
		InternalEnsureBufferSize(m_bitLength + 32);
		NetBitWriter.WriteUInt32((UInt32)source, 32, Data, m_bitLength);
		m_bitLength += 32;
	}
#endif

	[CLSCompliant(false)]
#if UNSAFE
		public unsafe void Write(UInt32 source)
		{
			InternalEnsureBufferSize(m_bitLength + 32);

			// can write fast?
			if (m_bitLength % 8 == 0)
			{
				fixed (byte* numRef = &Data[m_bitLength / 8])
				{
					*((uint*)numRef) = source;
				}
			}
			else
			{
				NetBitWriter.WriteUInt32(source, 32, Data, m_bitLength);
			}

			m_bitLength += 32;
		}
#else
	public void Write(UInt32 source)
	{
		InternalEnsureBufferSize(m_bitLength + 32);
		NetBitWriter.WriteUInt32(source, 32, Data, m_bitLength);
		m_bitLength += 32;
	}
#endif

	[CLSCompliant(false)]
	public void Write(UInt32 source, int numberOfBits)
	{
		Debug.Assert((numberOfBits > 0 && numberOfBits <= 32), "Write(uint, numberOfBits) can only write between 1 and 32 bits");
		InternalEnsureBufferSize(m_bitLength + numberOfBits);
		NetBitWriter.WriteUInt32(source, numberOfBits, Data, m_bitLength);
		m_bitLength += numberOfBits;
	}

	public void Write(Int32 source, int numberOfBits)
	{
		Debug.Assert((numberOfBits > 0 && numberOfBits <= 32), "Write(int, numberOfBits) can only write between 1 and 32 bits");
		InternalEnsureBufferSize(m_bitLength + numberOfBits);

		if (numberOfBits != 32)
		{
			// make first bit sign
			int signBit = 1 << (numberOfBits - 1);
			if (source < 0)
				source = (-source - 1) | signBit;
			else
				source &= (~signBit);
		}

		NetBitWriter.WriteUInt32((uint)source, numberOfBits, Data, m_bitLength);

		m_bitLength += numberOfBits;
	}

	//
	// 64 bit
	//
	[CLSCompliant(false)]
	public void Write(UInt64 source)
	{
		InternalEnsureBufferSize(m_bitLength + 64);
		NetBitWriter.WriteUInt64(source, 64, Data, m_bitLength);
		m_bitLength += 64;
	}

	[CLSCompliant(false)]
	public void Write(UInt64 source, int numberOfBits)
	{
		InternalEnsureBufferSize(m_bitLength + numberOfBits);
		NetBitWriter.WriteUInt64(source, numberOfBits, Data, m_bitLength);
		m_bitLength += numberOfBits;
	}

	public void Write(Int64 source)
	{
		InternalEnsureBufferSize(m_bitLength + 64);
		ulong usource = (ulong)source;
		NetBitWriter.WriteUInt64(usource, 64, Data, m_bitLength);
		m_bitLength += 64;
	}

	public void Write(Int64 source, int numberOfBits)
	{
		InternalEnsureBufferSize(m_bitLength + numberOfBits);
		ulong usource = (ulong)source;
		NetBitWriter.WriteUInt64(usource, numberOfBits, Data, m_bitLength);
		m_bitLength += numberOfBits;
	}

	//
	// Floating point
	//
#if UNSAFE
		public unsafe void Write(float source)
		{
			uint val = *((uint*)&source);
#if BIGENDIAN
				val = NetUtility.SwapByteOrder(val);
#endif
			Write(val);
		}
#else
	public void Write(float source)
	{
		byte[] val = BitConverter.GetBytes(source);
#if BIGENDIAN
			// swap byte order
			byte tmp = val[3];
			val[3] = val[0];
			val[0] = tmp;
			tmp = val[2];
			val[2] = val[1];
			val[1] = tmp;
#endif
		Write(val);
	}
#endif

#if UNSAFE
		public unsafe void Write(double source)
		{
			ulong val = *((ulong*)&source);
#if BIGENDIAN
			val = NetUtility.SwapByteOrder(val);
#endif
			Write(val);
		}
#else
	public void Write(double source)
	{
		byte[] val = BitConverter.GetBytes(source);
#if BIGENDIAN
			// 0 1 2 3   4 5 6 7

			// swap byte order
			byte tmp = val[7];
			val[7] = val[0];
			val[0] = tmp;

			tmp = val[6];
			val[6] = val[1];
			val[1] = tmp;

			tmp = val[5];
			val[5] = val[2];
			val[2] = tmp;

			tmp = val[4];
			val[4] = val[3];
			val[3] = tmp;
#endif
		Write(val);
	}
#endif

	//
	// Variable bits
	//

	/// <summary>
	/// Write Base128 encoded variable sized unsigned integer
	/// </summary>
	/// <returns>number of bytes written</returns>
	[CLSCompliant(false)]
	public int WriteVariableUInt32(uint value)
	{
		int retval = 1;
		uint num1 = (uint)value;
		while (num1 >= 0x80)
		{
			this.Write((byte)(num1 | 0x80));
			num1 = num1 >> 7;
			retval++;
		}
		this.Write((byte)num1);
		return retval;
	}

	/// <summary>
	/// Write Base128 encoded variable sized signed integer
	/// </summary>
	/// <returns>number of bytes written</returns>
	public int WriteVariableInt32(int value)
	{
		int retval = 1;
		uint num1 = (uint)((value << 1) ^ (value >> 31));
		while (num1 >= 0x80)
		{
			this.Write((byte)(num1 | 0x80));
			num1 = num1 >> 7;
			retval++;
		}
		this.Write((byte)num1);
		return retval;
	}

	/// <summary>
	/// Write Base128 encoded variable sized unsigned integer
	/// </summary>
	/// <returns>number of bytes written</returns>
	[CLSCompliant(false)]
	public int WriteVariableUInt64(UInt64 value)
	{
		int retval = 1;
		UInt64 num1 = (UInt64)value;
		while (num1 >= 0x80)
		{
			this.Write((byte)(num1 | 0x80));
			num1 = num1 >> 7;
			retval++;
		}
		this.Write((byte)num1);
		return retval;
	}

	/// <summary>
	/// Compress (lossy) a float in the range -1..1 using numberOfBits bits
	/// </summary>
	public void WriteSignedSingle(float value, int numberOfBits)
	{
		Debug.Assert(((value >= -1.0) && (value <= 1.0)), " WriteSignedSingle() must be passed a float in the range -1 to 1; val is " + value);

		float unit = (value + 1.0f) * 0.5f;
		int maxVal = (1 << numberOfBits) - 1;
		uint writeVal = (uint)(unit * (float)maxVal);

		Write(writeVal, numberOfBits);
	}

	/// <summary>
	/// Compress (lossy) a float in the range 0..1 using numberOfBits bits
	/// </summary>
	public void WriteUnitSingle(float value, int numberOfBits)
	{
		Debug.Assert(((value >= 0.0) && (value <= 1.0)), " WriteUnitSingle() must be passed a float in the range 0 to 1; val is " + value);

		int maxValue = (1 << numberOfBits) - 1;
		uint writeVal = (uint)(value * (float)maxValue);

		Write(writeVal, numberOfBits);
	}

	/// <summary>
	/// Compress a float within a specified range using a certain number of bits
	/// </summary>
	public void WriteRangedSingle(float value, float min, float max, int numberOfBits)
	{
		Debug.Assert(((value >= min) && (value <= max)), " WriteRangedSingle() must be passed a float in the range MIN to MAX; val is " + value);

		float range = max - min;
		float unit = ((value - min) / range);
		int maxVal = (1 << numberOfBits) - 1;
		Write((UInt32)((float)maxVal * unit), numberOfBits);
	}

	/// <summary>
	/// Writes an integer with the least amount of bits need for the specified range
	/// Returns number of bits written
	/// </summary>
	public int WriteRangedInteger(int min, int max, int value)
	{
		Debug.Assert(value >= min && value <= max, "Value not within min/max range!");

		uint range = (uint)(max - min);
		int numBits = NetUtility.BitsToHoldUInt(range);

		uint rvalue = (uint)(value - min);
		Write(rvalue, numBits);

		return numBits;
	}

	/// <summary>
	/// Write a string
	/// </summary>
	public void Write(string source)
	{
		if (string.IsNullOrEmpty(source))
		{
			InternalEnsureBufferSize(m_bitLength + 8);
			WriteVariableUInt32(0);
			return;
		}

		byte[] bytes = Encoding.UTF8.GetBytes(source);
		InternalEnsureBufferSize(m_bitLength + 1 + bytes.Length);
		WriteVariableUInt32((uint)bytes.Length);
		Write(bytes);
	}

	/// <summary>
	/// Write a string using the string table
	/// </summary>
	public void Write(NetConnection recipient, string str)
	{
		recipient.WriteStringTable(this, str);
	}

	/// <summary>
	/// Writes an IPv4 endpoint description
	/// </summary>
	/// <param name="endPoint"></param>
	public void Write(IPEndPoint endPoint)
	{
		Write(BitConverter.ToUInt32(endPoint.Address.GetAddressBytes(), 0));
		Write((ushort)endPoint.Port);
	}

	/// <summary>
	/// Pads data with enough bits to reach a full byte. Decreases cpu usage for subsequent byte writes.
	/// </summary>
	public void WritePadBits()
	{
		m_bitLength = ((m_bitLength + 7) / 8) * 8;
		EnsureBufferSize(m_bitLength);
	}

	/// <summary>
	/// Pads data with the specified number of bits.
	/// </summary>
	public void WritePadBits(int numberOfBits)
	{
		m_bitLength += numberOfBits;
		EnsureBufferSize(m_bitLength);
	}
}

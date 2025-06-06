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
using System.Net;

namespace Lidgren.Network;

internal sealed class IncomingNetMessage : NetMessage
{
	internal NetConnection m_sender;
	internal IPEndPoint m_senderEndPoint;

	/// <summary>
	/// Read this message from the packet buffer
	/// </summary>
	/// <returns>new read pointer position</returns>
	internal void ReadFrom(NetBuffer buffer, IPEndPoint endpoint)
	{
		m_senderEndPoint = endpoint;

		// read header
		byte header = buffer.ReadByte();
		m_type = (NetMessageLibraryType)(header & 7);
		m_sequenceChannel = (NetChannel)(header >> 3);
		m_sequenceNumber = buffer.ReadUInt16();

		int payLen = (int)buffer.ReadVariableUInt32();

		// copy payload into message buffer
		m_data.EnsureBufferSize(payLen * 8);
		buffer.ReadBytes(m_data.Data, 0, payLen);
		m_data.Reset(0, payLen * 8);

		return;
	}

	public override string ToString()
	{
		if (m_type == NetMessageLibraryType.System)
			return "[Incoming " + (NetSystemType)m_data.Data[0] + " " + m_sequenceChannel + "|" + m_sequenceNumber + "]";

		return "[Incoming " + m_type + " " + m_sequenceChannel + "|" + m_sequenceNumber + "]";
	}
}

internal sealed class OutgoingNetMessage : NetMessage
{
	internal int m_numSent;
	internal double m_nextResend;
	internal NetBuffer m_receiptData;

	internal void Encode(NetBuffer intoBuffer)
	{
		Debug.Assert(m_sequenceNumber != -1);

		// message type, netchannel and sequence number
		intoBuffer.Write((byte)((int)m_type | ((int)m_sequenceChannel << 3)));
		intoBuffer.Write((ushort)m_sequenceNumber);

		// payload length
		int len = m_data.LengthBytes;
		intoBuffer.WriteVariableUInt32((uint)len);

		// copy payload
		intoBuffer.Write(m_data.Data, 0, len);

		return;
	}

	public override string ToString()
	{
		if (m_type == NetMessageLibraryType.System)
			return "[Outgoing " + (NetSystemType)m_data.Data[0] + " " + m_sequenceChannel + "|" + m_sequenceNumber + "]";

		return "[Outgoing " + m_type + " " + m_sequenceChannel + "|" + m_sequenceNumber + "]";
	}
}

abstract class NetMessage
{
	internal NetMessageType m_msgType;

	internal NetMessageLibraryType m_type;
	internal NetChannel m_sequenceChannel;
	internal int m_sequenceNumber = -1;

	internal NetBuffer m_data;

	public NetMessage()
	{
		m_msgType = NetMessageType.Data;
	}
}

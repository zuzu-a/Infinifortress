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

internal sealed class FragmentedMessage
{
	public int TotalFragments;
	public int FragmentsReceived;
	public int ChunkSize;
	public int BitLength;
	public byte[] Data;
}

public sealed partial class NetConnection
{
	private ushort m_nextSendFragmentId;

	/// <summary>
	/// Identifier : Complete byte array
	/// </summary>
	private Dictionary<int, FragmentedMessage> m_fragments;

	private void InitializeFragmentation()
	{
		m_fragments = new Dictionary<int, FragmentedMessage>();
		m_nextSendFragmentId = 1;
	}

	/// <summary>
	/// Called when a message should be released to the application
	/// </summary>
	private void AcceptMessage(IncomingNetMessage msg)
	{
		if (msg.m_type == NetMessageLibraryType.UserFragmented)
		{
			// parse
			int id = msg.m_data.ReadUInt16();
			int number = (int)msg.m_data.ReadVariableUInt32(); // 0 to total-1
			int total = (int)msg.m_data.ReadVariableUInt32();

			int bytePtr = msg.m_data.Position / 8;
			int payloadLen = msg.m_data.LengthBytes - bytePtr;

			FragmentedMessage fmsg;
			if (!m_fragments.TryGetValue(id, out fmsg))
			{
				if (number == total - 1)
				{
					// last message arrived first; ignore this
					return;
				}

				fmsg = new FragmentedMessage();
				fmsg.TotalFragments = total;
				fmsg.FragmentsReceived = 0;
				fmsg.ChunkSize = payloadLen;
				fmsg.Data = new byte[payloadLen * total];
				m_fragments[id] = fmsg;
			}

			// insert this fragment
			Array.Copy(
				msg.m_data.Data,
				bytePtr,
				fmsg.Data,
				number * fmsg.ChunkSize,
				payloadLen
			);

			fmsg.BitLength += (msg.m_data.m_bitLength - msg.m_data.Position);
			fmsg.FragmentsReceived++;

			m_owner.LogVerbose("Fragment " + id + " - " + (number + 1) + "/" + total + " received; chunksize " + fmsg.ChunkSize + " this size " + payloadLen, this);

			if (fmsg.FragmentsReceived < fmsg.TotalFragments)
			{
				// Not yet complete
				return;
			}

			// Done! Release it as a complete message
			NetBuffer buf = new NetBuffer(false);
			buf.Data = fmsg.Data;

			//int bitLen = (fmsg.TotalFragments - 1) * fmsg.ChunkSize * 8;
			//bitLen += msg.m_data.m_bitLength - (bytePtr * 8);

			buf.m_bitLength = fmsg.BitLength;

			m_fragments.Remove(id);

			// reuse "msg"
			m_owner.LogVerbose("All fragments received; complete length is " + buf.LengthBytes, this);
			msg.m_data = buf;
		}

		// release
		// m_owner.LogVerbose("Accepted " + msg, this);

		// Debug.Assert(msg.m_type == NetMessageLibraryType.User);

		// do custom handling on networking thread
		m_owner.ProcessReceived(msg.m_data);
		m_owner.EnqueueReceivedMessage(msg);
	}
}

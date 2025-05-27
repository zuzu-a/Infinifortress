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

public sealed partial class NetConnection
{
	/// <summary>
	/// Approves the connection and sends any (already set) local hail data
	/// </summary>
	public void Approve()
	{
		Approve(null);
	}

	/// <summary>
	/// Approves the connection and sents/sends local hail data provided
	/// </summary>
	public void Approve(byte[] localHailData)
	{
		if (m_approved == true)
			throw new NetException("Connection is already approved!");

		//
		// Continue connection phase
		//

		if (localHailData != null)
			m_localHailData = localHailData;

		// Add connection
		m_approved = true;

		NetServer server = m_owner as NetServer;
		server.AddConnection(NetTime.Now, this);
	}

	/// <summary>
	/// Disapprove the connection, rejecting it.
	/// </summary>
	public void Disapprove(string reason)
	{
		if (m_approved == true)
			throw new NetException("Connection is already approved!");

		// send connectionrejected
		NetBuffer buf = new NetBuffer(reason);
		m_owner.QueueSingleUnreliableSystemMessage(
			NetSystemType.ConnectionRejected,
			buf,
			m_remoteEndPoint,
			false
		);

		m_requestDisconnect = true;
		m_requestLinger = 0.0f;
		m_requestSendGoodbye = !string.IsNullOrEmpty(reason);
		m_futureDisconnectReason = reason;
	}
}

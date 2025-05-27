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

namespace Lidgren.Network.Tests;

public class LidgrenNetworkTests
{
    [Fact]
    public void Write_Read_Test()
    {
        // JIT stuff
        NetBuffer msg = new NetBuffer(20);
        msg.Write((short)short.MaxValue);

        // Go
        double timeStart = NetTime.Now;

        msg = new NetBuffer(20);
        for (int n = 0; n < 10000; n++)
        {
            msg.Reset();

            msg.Write((short)short.MaxValue);
            msg.Write((short)short.MinValue);
            msg.Write((short)-42);

            msg.Write(421);
            msg.Write((byte)7);
            msg.Write(-42.8f);

            Assert.Equal(15, msg.LengthBytes);  //  Bad Message Length

            msg.Write("duke of earl");

            int bytesWritten;
            bytesWritten = msg.WriteVariableInt32(-1);
            bytesWritten = msg.WriteVariableInt32(5);
            bytesWritten = msg.WriteVariableInt32(-18);
            bytesWritten = msg.WriteVariableInt32(42);
            bytesWritten = msg.WriteVariableInt32(-420);

            msg.Write((uint)9991);

            // byte boundary kept until here

            msg.Write(true);
            msg.Write((uint)3, 5);
            msg.Write(8.111f);
            msg.Write("again");
            byte[] arr = new byte[] { 1, 6, 12, 24 };
            msg.Write(arr);
            msg.Write((byte)7, 7);
            msg.Write(Int32.MinValue);
            msg.Write(UInt32.MaxValue);
            msg.WriteRangedSingle(21.0f, -10, 50, 12);

            // test reduced bit signed writing
            msg.Write(15, 5);
            msg.Write(2, 5);
            msg.Write(0, 5);
            msg.Write(-1, 5);
            msg.Write(-2, 5);
            msg.Write(-15, 5);

            msg.Write(UInt64.MaxValue);
            msg.Write(Int64.MaxValue);
            msg.Write(Int64.MinValue);

            msg.Write(42);
            msg.WritePadBits();

            int numBits = msg.WriteRangedInteger(0, 10, 5);
            Assert.Equal(4, numBits);  //  Ack WriteRangedInteger Failed

            // verify
            msg.Position = 0;
            Assert.Equal(short.MaxValue, msg.ReadInt16());  //  Ack thpth short failed
            Assert.Equal(short.MinValue, msg.ReadInt16());  //  Ack thpth short failed
            Assert.Equal(-42, msg.ReadInt16());             //  Ack thpth short failed.

            Assert.Equal(421, msg.ReadInt32());              //  Ack thphth 1
            Assert.Equal((byte)7, msg.ReadByte());           //  Ack thphth 2
            Assert.Equal(-42.8f, msg.ReadSingle());          //  Ack thphth 3
            Assert.Equal("duke of earl", msg.ReadString());  //  Ack thphth 4
            Assert.Equal(-1, msg.ReadVariableInt32());       //  ReadVariableInt32 failed 1
            Assert.Equal(5, msg.ReadVariableInt32());        //  ReadVariableInt32 failed 2
            Assert.Equal(-18, msg.ReadVariableInt32());      //  ReadVariableInt32 failed 3
            Assert.Equal(42, msg.ReadVariableInt32());       //  ReadVariableInt32 failed 4
            Assert.Equal(-420, msg.ReadVariableInt32());     //  ReadVariableInt32 failed 5

            Assert.Equal((uint)9991, msg.ReadUInt32());  //  Ack thpth 4.5

            Assert.True(msg.ReadBoolean());            // Ack thpth 5
            Assert.Equal((uint)3, msg.ReadUInt32(5));  //  Ack thphth 6
            Assert.Equal(8.111f, msg.ReadSingle());    //  Ack thpth 7
            Assert.Equal("again", msg.ReadString());   //  Ack thphth 8

            Assert.Equal(msg.ReadBytes(4), arr);              //  Ack thphth 9
            Assert.Equal((byte)7, msg.ReadByte(7));           //  Ack thphth 10
            Assert.Equal(Int32.MinValue, msg.ReadInt32());    //  Ack thphth 11
            Assert.Equal(UInt32.MaxValue, msg.ReadUInt32());  //  Ack thphth 12

            float v = msg.ReadRangedSingle(-10, 50, 12);

            // v should be close to, but not necessarily exactly, 21.0f
            Assert.True((float)Math.Abs(21.0f - v) <= 0.1f);  //  Ack thphth *RangedSingle() failed

            Assert.Equal(15, msg.ReadInt32(5));   //  Ack thphth RedInt32 1
            Assert.Equal(2, msg.ReadInt32(5));    //  Ack thphth RedInt32 2
            Assert.Equal(0, msg.ReadInt32(5));    //  Ack thphth RedInt32 3
            Assert.Equal(-1, msg.ReadInt32(5));   //  Ack thphth RedInt32 4
            Assert.Equal(-2, msg.ReadInt32(5));   //  Ack thphth RedInt32 5
            Assert.Equal(-15, msg.ReadInt32(5));  //  Ack thphth RedInt32 6

            Assert.Equal(UInt64.MaxValue, msg.ReadUInt64());  //  Ack thphth Uint64
            Assert.Equal(Int64.MaxValue, msg.ReadInt64());    //  Ack thphth Int64
            Assert.Equal(Int64.MinValue, msg.ReadInt64());    //  Ack thphth Int64

            Assert.Equal(42, msg.ReadInt32());  //  Ack thphth end

            msg.SkipPadBits();

            Assert.Equal(5, msg.ReadRangedInteger(0, 10));  //  Ack thphth ranged integer
        }
    }

    [Fact]
    public void Write_Variable_Uint64_Test()
    {
        NetBuffer largeBuffer = new NetBuffer(100 * 8);
        UInt64[] largeNumbers = new ulong[100];

        for (int i = 0; i < 100; i++)
        {
            largeNumbers[i] = ((ulong)NetRandom.Instance.NextUInt() << 32) | (ulong)NetRandom.Instance.NextUInt();
            largeBuffer.WriteVariableUInt64(largeNumbers[i]);
        }

        largeBuffer.Position = 0;
        for (int i = 0; i < 100; i++)
        {
            UInt64 ln = largeBuffer.ReadVariableUInt64();
            Assert.Equal(largeNumbers[i], ln);  //  Large Fail
        }
    }

    [Fact]
    public void Write_Pad_Bits_Test()
    {
        for (int i = 1; i < 31; i++)
        {
            NetBuffer buf = new NetBuffer();
            buf.Write((int)1, i);

            Assert.Equal(i, buf.LengthBits);    //  Bad Length

            buf.WritePadBits();
            int wholeBytes = buf.LengthBits / 8;

            Assert.Equal(wholeBytes * 8, buf.LengthBits);   //  WritePadbits failed
        }
    }

    [Fact]
    public void Small_Allocation_Test()
    {
        NetBuffer small = new NetBuffer(100);
        byte[] rnd = new byte[24];
        int[] bits = new int[24];
        for (int i = 0; i < 24; i++)
        {
            rnd[i] = (byte)NetRandom.Instance.Next(0, 65);
            bits[i] = NetUtility.BitsToHoldUInt((uint)rnd[i]);

            small.Write(rnd[i], bits[i]);
        }

        small.Position = 0;
        for (int i = 0; i < 24; i++)
        {
            byte got = small.ReadByte(bits[i]);
            Assert.Equal(rnd[i], got);  //  Failed small allocation test
        }
    }

    [Fact]
    public void Client_Server_Test()
    {
        NetConfiguration config = new NetConfiguration("unittest");
        config.Port = 14242;
        NetServer server = new NetServer(config);
        NetBuffer serverBuffer = new NetBuffer();
        server.Start();

        config = new NetConfiguration("unittest");
        NetClient client = new NetClient(config);
        client.SetMessageTypeEnabled(NetMessageType.Receipt, true);
        NetBuffer clientBuffer = client.CreateBuffer();
        client.Start();

        client.Connect("127.0.0.1", 14242);

        List<string> events = new List<string>();

        double end = double.MaxValue;
        double disconnect = double.MaxValue;

        while (NetTime.Now < end)
        {
            double now = NetTime.Now;

            NetMessageType nmt;
            NetConnection sender;

            //
            // client
            //
            if (client.ReadMessage(clientBuffer, out nmt))
            {
                switch (nmt)
                {
                    case NetMessageType.StatusChanged:
                        Console.WriteLine("Client: " + client.Status + " (" + clientBuffer.ReadString() + ")");
                        events.Add("CStatus " + client.Status);
                        if (client.Status == NetConnectionStatus.Connected)
                        {
                            // send reliable message
                            NetBuffer buf = client.CreateBuffer();
                            buf.Write(true);
                            buf.Write((int)52, 7);
                            buf.Write("Hallon");

                            client.SendMessage(buf, NetChannel.ReliableInOrder1, new NetBuffer("kokos"));
                        }

                        if (client.Status == NetConnectionStatus.Disconnected)
                            end = NetTime.Now + 1.0; // end in one second

                        break;
                    case NetMessageType.Receipt:
                        events.Add("CReceipt " + clientBuffer.ReadString());
                        break;
                    case NetMessageType.ConnectionRejected:
                    case NetMessageType.BadMessageReceived:
                        Assert.Fail("Failed: " + nmt);
                        break;
                    case NetMessageType.DebugMessage:
                        // silently ignore
                        break;
                    default:
                        // ignore
                        Console.WriteLine("Ignored: " + nmt);
                        break;
                }
            }

            //
            // server
            //
            if (server.ReadMessage(serverBuffer, out nmt, out sender))
            {
                switch (nmt)
                {
                    case NetMessageType.StatusChanged:
                        events.Add("SStatus " + sender.Status);
                        Console.WriteLine("Server: " + sender.Status + " (" + serverBuffer.ReadString() + ")");
                        break;
                    case NetMessageType.ConnectionRejected:
                    case NetMessageType.BadMessageReceived:
                        Assert.Fail("Failed: " + nmt);
                        break;
                    case NetMessageType.Data:
                        events.Add("DataRec " + serverBuffer.LengthBits);
                        bool shouldBeTrue = serverBuffer.ReadBoolean();
                        int shouldBeFifthTwo = serverBuffer.ReadInt32(7);
                        string shouldBeHallon = serverBuffer.ReadString();

                        Assert.True(shouldBeTrue);
                        Assert.Equal(52, shouldBeFifthTwo);
                        Assert.Equal("Hallon", shouldBeHallon);

                        disconnect = now + 1.0;
                        break;
                    case NetMessageType.DebugMessage:
                        // silently ignore
                        break;
                    default:
                        // ignore
                        Console.WriteLine("Ignored: " + nmt);
                        break;
                }
            }

            if (now > disconnect)
            {
                server.Connections[0].Disconnect("Bye", 0.1f);
                disconnect = double.MaxValue;
            }
        }

        // verify events
        string[] expected = new string[] {
                "CStatus Connecting",
                "SStatus Connecting",
                "CStatus Connected",
                "SStatus Connected",
                "DataRec 64",
                "CReceipt kokos",
                "SStatus Disconnecting",
                "CStatus Disconnecting",
                "SStatus Disconnected",
                "CStatus Disconnected"
            };

        Assert.Equal(expected, events);

        server.Shutdown("Test App Existing");
        client.Shutdown("Test App Existing");
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using FagNet.Core.Constants;
using FagNet.Core.Constants.Packets;
using FagNet.Core.Cryptography;
using FagNet.Core.Data;

namespace FagNet.Core.Network
{
    public class Packet
    {
        protected BinaryReader _r;
        protected BinaryWriter _w;
        protected byte _packetID;

        public byte PacketID { get { return _packetID; } }

        protected Packet()
        {

        }

        public Packet(byte packetID)
        {
            _w = new BinaryWriter(new MemoryStream());
            _w.Write((byte)0xF0);
            _w.Write(packetID);
            _packetID = packetID;
        }
        public Packet(EAuthPacket packetID) : this((byte)packetID) { }
        public Packet(EChatPacket packetID) : this((byte)packetID) { }
        public Packet(ERelayPacket packetID) : this((byte)packetID) { }
        public Packet(EGamePacket packetID) : this((byte)packetID) { }
        public Packet(ENATPacket packetID) : this((byte)packetID) { }

        public Packet(byte[] data, int offset = 0)
        {
            _r = new BinaryReader(new MemoryStream(data));
            _r.ReadBytes(offset);
            _r.ReadByte();
            _packetID = _r.ReadByte();
        }

        ~Packet()
        {
            if (_r != null)
            {
                _r.Dispose();
                _r = null;
            }
            if (_w != null)
            {
                _w.Dispose();
                _w = null;
            }
        }

        public virtual byte[] GetData()
        {
            if (_r != null)
                return ((MemoryStream) _r.BaseStream).ToArray();
            if (_w == null) return null;
            var data = ((MemoryStream) _w.BaseStream).ToArray();
            var size = (ushort)(data.Length + 2);
            var ret = new byte[data.Length + 2];
            Array.Copy(BitConverter.GetBytes(size), ret, 2);
            Array.Copy(data, 0, ret, 2, data.Length);
            return ret;
        }

        public virtual void Decrypt()
        {
            if (_r == null)
                return;
            var data = ((MemoryStream) _r.BaseStream).ToArray();
            S4Crypt.Decrypt(data, 2);
            _r.Close();
            _r.Dispose();
            _r = new BinaryReader(new MemoryStream(data));
            _r.ReadByte();
            _packetID = _r.ReadByte();
        }

        public virtual void Write(byte value)
        {
            _w.Write(value);
        }
        public virtual void Write(params byte[] value)
        {
            _w.Write(value);
        }

        public virtual void Write(UInt16 value)
        {
            _w.Write(value);
        }
        public virtual void Write(params UInt16[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(Int16 value)
        {
            _w.Write(value);
        }
        public virtual void Write(params Int16[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(UInt32 value)
        {
            _w.Write(value);
        }
        public virtual void Write(params UInt32[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(Int32 value)
        {
            _w.Write(value);
        }
        public virtual void Write(params Int32[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(UInt64 value)
        {
            _w.Write(value);
        }
        public virtual void Write(params UInt64[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(Int64 value)
        {
            _w.Write(value);
        }
        public virtual void Write(params Int64[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(float value)
        {
            _w.Write(value);
        }
        public virtual void Write(params float[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(double value)
        {
            _w.Write(value);
        }
        public virtual void Write(params double[] value)
        {
            foreach (var val in value)
                Write(val);
        }

        public virtual void Write(bool value)
        {
            if (value)
                Write((byte)0x01);
            else
                Write((byte)0x00);
        }

        public virtual void Write(string value, Encoding encoding = null, bool nullTerminator = true)
        {
            if (encoding == null)
                encoding = Encoding.ASCII;
            if (nullTerminator)
                value += "\0";

            var tmp = encoding.GetBytes(value);
            _w.Write(tmp);
        }
        public virtual void WriteStringBuffer(string value, int length, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.ASCII;

            var tmp = encoding.GetBytes(value);
            var tmp2 = new byte[length];
            if (tmp.Length > length)
                return;

            Array.Copy(tmp, tmp2, tmp.Length);
            _w.Write(tmp2);
        }

        public void WriteChatUserData(Player plr, bool shortVersion = false)
        {
            if (!shortVersion)
            {
                Write(plr.AccountID);
                WriteStringBuffer(plr.Nickname, 31);
            }
            Write((ushort)2); // unk
            Write(plr.AccountID);
            Write(plr.ServerID);
            Write(plr.Channel != null ? (short)plr.Channel.ID : (short)-1);
            Write((plr.Room == null || plr.Room.ID == 0) ? -1 : (int)plr.Room.ID);
            Write(plr.CommunityByte);
            Write((uint)plr.CalculateTotalEXP());

            // 32 bytes
            Write((float)plr.TDStats.CalculateTotalScore()); // total td score
            Write((uint)0); // unk
            Write(plr.TDStats.CalculateOffensePerMatch());
            Write(plr.TDStats.CalculateDefensePerMatch());
            Write(plr.TDStats.CalculateRecoveryPerMatch());
            Write(plr.TDStats.CalculateWinRate()); // td lose rate %
            Write(1337f); // K/D rate
            Write(1337f); // dm lose rate %


            Write((byte)plr.AllowCombiRequest);
            Write((byte)plr.AllowFriendRequest);
            Write((byte)plr.AllowInvite);
            Write((byte)plr.AllowInfoRequest);

            // 41 bytes
            Write(plr.CommunityData);
        }
        public void WriteEventMessage(ulong unk1, EPlayerEventMessage msg, string str = null, uint unk2 = 0, ushort unk3 = 0)
        {
            var strLen = (uint)(string.IsNullOrEmpty(str) ? 0 : (str.Length + 1));
            _w.Write((byte)msg);
            _w.Write(unk1);
            _w.Write(unk2);
            _w.Write(unk3);
            _w.Write(strLen);
            if (strLen > 0)
                _w.Write(str);
        }

        public virtual bool ReadBoolean()
        {
            return _r.ReadBoolean();
        }
        public virtual byte ReadByte()
        {
            return _r.ReadByte();
        }
        public virtual byte[] ReadBytes(int length)
        {
            return _r.ReadBytes(length);
        }

        public virtual UInt16 ReadUInt16()
        {
            return _r.ReadUInt16();
        }
        public virtual Int16 ReadInt16()
        {
            return _r.ReadInt16();
        }

        public virtual UInt32 ReadUInt32()
        {
            return _r.ReadUInt32();
        }
        public virtual Int32 ReadInt32()
        {
            return _r.ReadInt32();
        }

        public virtual UInt64 ReadUInt64()
        {
            return _r.ReadUInt64();
        }
        public virtual Int64 ReadInt64()
        {
            return _r.ReadInt64();
        }

        public virtual float ReadFloat()
        {
            return _r.ReadSingle();
        }
        public virtual double ReadDouble()
        {
            return _r.ReadDouble();
        }

        public virtual byte[] ReadToEnd()
        {
            return _r.ReadBytes((int)_r.BaseStream.Length - (int)_r.BaseStream.Position);
        }

        public virtual string ReadCString()
        {
            var tmp = new List<byte>();
            while (true)
            {
                var b = _r.ReadByte();
                if (b == 0x00)
                    break;
                tmp.Add(b);
            }

            return Encoding.ASCII.GetString(tmp.ToArray());
        }

        public virtual string ReadCStringBuffer(int length)
        {
            var str = ReadCString();
            _r.ReadBytes(length - str.Length - 1);
            return str;
        }
    }
}

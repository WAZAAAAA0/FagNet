using System.IO;
using System.Runtime.InteropServices;

using FagNet.Core.Constants.Packets;

namespace FagNet.Core.Network
{
    public class P2PPacket : Packet
    {
        //[DllImport("S4Compression.dll")]
        //protected static extern short compressFloat(float value);

        //[DllImport("S4Compression.dll")]
        //protected static extern float decompressFloat(short value);

        protected ushort _port;
        protected uint _ip;
        protected ushort _unk;
        protected new EP2PPacket _packetID;
        protected byte _slot;
        protected ushort _size;

        public ushort Port { get { return _port; } }
        public uint IP { get { return _ip; } }
        public ushort Unk { get { return _unk; } }
        public new EP2PPacket PacketID { get { return _packetID; } }
        public byte Slot { get { return _slot; } }
        public ushort Size { get { return _size; } }

        //public P2PPacket(EP2PPacket packetID, byte slot, uint ip = 1, ushort port = 1, ushort unk = 0)
        //    : this((byte)packetID, slot, ip, port, unk)
        //{

        //}

        //public P2PPacket(byte packetID, byte slot, uint ip = 1, ushort port = 1, ushort unk = 0)
        //{
        //    _w = new BinaryWriter(new MemoryStream());
        //    _ip = ip;
        //    _port = port;
        //    _unk = unk;
        //    _packetID = (EP2PPacket)packetID;
        //    _slot = slot;
        //}

        public P2PPacket(byte[] data, int offset = 0)
        {
            _r = new BinaryReader(new MemoryStream(data));
            _r.ReadBytes(offset);
            _port = _r.ReadUInt16();
            _ip = _r.ReadUInt32();
            _unk = _r.ReadUInt16();
            _packetID = (EP2PPacket)_r.ReadByte();
            _slot = _r.ReadByte();
            _size = _r.ReadUInt16();
        }

        //public override byte[] GetData()
        //{
        //    if (_r != null)
        //        return ((MemoryStream)_r.BaseStream).ToArray();
        //    if (_w == null) return null;
        //    var data = ((MemoryStream)_w.BaseStream).ToArray();
        //    var size = (ushort)(data.Length + 12);

        //    using (var w = new BinaryWriter(new MemoryStream()))
        //    {
        //        w.Write((uint)1);
        //        w.Write((uint)1 | 0x5A00000);
        //        w.Write((byte)_packetID);
        //        w.Write(_slot);
        //        w.Write(size);
        //        w.Write(data);

        //        return ((MemoryStream)w.BaseStream).ToArray();
        //    }
        //}

        //public virtual void WriteCompressed(float value)
        //{
        //    base.Write(compressFloat(value));
        //}

        //public virtual float ReadCompressedFloat()
        //{
        //    return decompressFloat(base.ReadInt16());
        //}
    }
}

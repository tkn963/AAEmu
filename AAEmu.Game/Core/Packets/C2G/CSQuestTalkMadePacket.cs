using System.Threading.Tasks;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSQuestTalkMadePacket : GamePacket
    {
        private uint _npcObjId;
        private uint _questContextId;
        private uint _questCompId;
        private uint _questActId;

        public CSQuestTalkMadePacket() : base(CSOffsets.CSQuestTalkMadePacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            _npcObjId = stream.ReadBc();
            _questContextId = stream.ReadUInt32();
            _questCompId = stream.ReadUInt32();
            _questActId = stream.ReadUInt32();
        }
        public override async Task Execute()
        {
            _log.Warn("QuestTalkMade, ObjId: {0}, QctxId: {1}, QcompId: {2}, QactId: {3}", _npcObjId, _questContextId, _questCompId, _questActId);
            Connection.ActiveChar.Quests?.OnTalkMade(_npcObjId, _questContextId, _questCompId, _questActId);
        }
    }
}

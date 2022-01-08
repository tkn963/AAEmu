﻿using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActConAcceptNpcEmotion : QuestActTemplate
    {
        public uint NpcId { get; set; }
        public string Emotion { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActConAcceptNpcEmotion: NpcId {0}, Emotion {1}", NpcId, Emotion);

            if (!(character.CurrentTarget is Npc))
                return false;

            quest.QuestAcceptorType = QuestAcceptorType.Npc;
            quest.AcceptorType = NpcId;

            return ((Npc)character.CurrentTarget).TemplateId == NpcId;
        }
    }
}

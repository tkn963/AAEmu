using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Items;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjItemGather : QuestActTemplate, IQuestActScoreProvider // Сбор предметов
    {
        public uint ItemId { get; set; }
        public int Count { get; set; }
        public uint HighlightDoodadId { get; set; }
        public int HighlightDoodadPhase { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }
        public bool Cleanup { get; set; }
        public bool DropWhenDestroy { get; set; }
        public bool DestroyWhenDrop { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjItemGather: QuestActObjItemGatherId {0}, Count {1}, UseAlias {2}, QuestActObjAliasId {3}, HighlightDoodadId {4}, HighlightDoodadPhase {5}, quest {6}, objective {7}, Score {8}",
                ItemId, Count, UseAlias, QuestActObjAliasId, HighlightDoodadId, HighlightDoodadPhase, quest.TemplateId, objective, quest.Template.Score);

            if (character.Inventory.CheckItems(SlotType.Inventory, ItemId, Count))
                return true;

            _log.Debug(objective + "   |THIS|   " + Count);

            return objective >= Count;
        }

        public int GetScoreForObjective(int objective)
        {
            return objective * Count;
        }
    }
}

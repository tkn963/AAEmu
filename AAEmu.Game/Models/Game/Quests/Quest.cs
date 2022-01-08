using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.Quests
{
    public class Quest : PacketMarshaler
    {
        private const int ObjectiveCount = 5;
        public long Id { get; set; }
        public uint TemplateId { get; set; }
        public QuestTemplate Template { get; set; }
        public QuestStatus Status { get; set; }
        private int[] Objectives { get; set; }
        public QuestComponentKind Step { get; set; }
        public DateTime Time { get; set; }
        public Character Owner { get; set; }
        public int LeftTime => Time > DateTime.UtcNow ? (int)(Time - DateTime.UtcNow).TotalSeconds : -1;
        public int SupplyItem { get; set; }
        public bool EarlyCompletion { get; set; }
        public long DoodadId { get; set; }
        public ulong ObjId { get; set; }
        public uint ComponentId { get; set; }
        public QuestAcceptorType QuestAcceptorType { get; set; }
        public uint AcceptorType { get; set; }

        public uint GetActiveComponent()
        {
            return Template.GetComponent(Step).Id;
        }

        public Quest()
        {
            Objectives = new int[ObjectiveCount];
            SupplyItem = 0;
            EarlyCompletion = false;
            ObjId = 0;
        }

        public Quest(QuestTemplate template)
        {
            TemplateId = template.Id;
            Template = template;
            Objectives = new int[ObjectiveCount];
            SupplyItem = 0;
            EarlyCompletion = false;
            ObjId = 0;
        }

        public uint Start()
        {
            var res = false;
            //ComponentId = 0u;

            for (Step = QuestComponentKind.None; Step <= QuestComponentKind.Ready; Step++) // далее шага Ready = 6 не идем
            {
                if (Step >= QuestComponentKind.Ready)
                    Status = QuestStatus.Ready;

                var components = Template.GetComponents(Step); // взять текущий компонент для анализа, может быть более одного
                if (components.Count == 0)
                    continue; // пропускаем пустые компоненты

                for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {
                    var acts = QuestManager.Instance.GetActs(components[componentIndex].Id); // взять акт для анализа, может быть более одного
                    foreach (var act in acts)
                    {
                        switch (act.DetailType)
                        {
                            case "QuestActSupplyItem": // when Step == QuestComponentKind.Supply:
                                //res = act.Use(Owner, this, SupplyItem);
                                var tmp = ComponentId;
                                res = act.Use(Owner, this, 0);  // получим квестовый предмет без взаимодействия с кем либо, если objective = 0, то добавим в инвентарь предмет или багаж
                                ComponentId = tmp; // восстановим сброшенный ComponentId
                                break;
                            case "QuestActObjItemUse" when Step == QuestComponentKind.Progress:
                                {
                                    var template = act.GetTemplate<QuestActObjItemUse>();
                                    // TODO: Check both inventory and warehouse
                                    Status = QuestStatus.Progress;
                                    Owner.Inventory.Bag.GetAllItemsByTemplate(template.Id, -1, out _, out var objectivesCounted);
                                    Objectives[componentIndex] = objectivesCounted;
                                    //Objectives[componentIndex] = Owner.Inventory.GetItemsCount(template.ItemId);
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        Objectives[componentIndex] = template.Count;
                                        Status = QuestStatus.Ready;
                                    }

                                    _log.Warn("Quest - {0}: ComponentId {1}, Step {2}, Status {3}, res {4}, act.DetailType {5}", TemplateId, ComponentId, Step, Status, res, act.DetailType); //  for debuging

                                    res = act.Use(Owner, this, Objectives[componentIndex]);
                                    break;
                                }
                            case "QuestActObjItemGather" when Step == QuestComponentKind.Progress: // проверка на то, что в инвентаре уже есть нужный квестовый предмет
                                {
                                    var template = act.GetTemplate<QuestActObjItemGather>();
                                    // TODO: Check both inventory and warehouse
                                    Status = QuestStatus.Progress;
                                    Owner.Inventory.Bag.GetAllItemsByTemplate(template.Id, -1, out _, out var objectivesCounted);
                                    Objectives[componentIndex] = objectivesCounted;
                                    //Objectives[componentIndex] = Owner.Inventory.GetItemsCount(template.ItemId);
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        Objectives[componentIndex] = template.Count;
                                        Status = QuestStatus.Ready;
                                    }

                                    _log.Warn("Quest - {0}: ComponentId {1}, Step {2}, Status {3}, res {4}, act.DetailType {5}", TemplateId, ComponentId, Step, Status, res, act.DetailType); //  for debuging

                                    res = act.Use(Owner, this, Objectives[componentIndex]);
                                    break;
                                }
                            case "QuestActObjInteraction":
                            case "QuestActObjMonsterGroupHunt": // подсчитывает квест итемы
                            case "QuestActObjMonsterHunt":      // подсчитывает убийство мобов
                                Status = QuestStatus.Progress;
                                res = false; // заканчиваем цикл
                                break;
                            case "QuestActConReportNpc":
                                Status = QuestStatus.Ready;
                                res = false; // заканчиваем цикл
                                break;
                            case "QuestActCheckTimer":
                                res = true; // TODO ограничение времени на квест?
                                break;
                            default:
                                // case "QuestActConAcceptDoodad"
                                // case "QuestActConAcceptNpc" when Step == QuestComponentKind.Start:
                                // res = true, если взаимодействуем с нужным Npc, иначе false и цикл прерывается
                                res = act.Use(Owner, this, Objectives[componentIndex]); // заполним переменные QuestAcceptorType и AcceptorType
                                ComponentId = components[componentIndex].Id; // сохраняем только старовый ComponentId!
                                break;
                        }

                        _log.Warn("Quest - {0}: ComponentId {1}, Step {2}, Status {3}, res {4}, act.DetailType {5}", TemplateId, ComponentId, Step, Status, res, act.DetailType); //  for debuging
                    }
                }
                if (!res) // прерываем цикл если res = false
                    return ComponentId;
            }
            return res ? ComponentId : 0;
        }

        public void Update(bool send = true)
        {
            if (!send) { return; }

            var res = false;
            //ComponentId = 0u;
            for (; Step <= QuestComponentKind.Reward; Step++)
            {
                switch (Step)
                {
                    case QuestComponentKind.Reward:
                        Status = QuestStatus.Completed;
                        Owner.Quests.Complete(TemplateId, 0, false);
                        return;
                    case >= QuestComponentKind.Drop:
                        Status = QuestStatus.Completed;
                        break;
                    case >= QuestComponentKind.Ready:
                        Status = QuestStatus.Ready;
                        break;
                }

                var components = Template.GetComponents(Step);

                //if (Step == QuestComponentKind.Progress && Template.Score > 0)
                //{
                //    if (GetScore(components) >= Template.Score)
                //        Status = QuestStatus.Ready;
                //}

                switch (components.Count)
                {
                    //case 0 when Step == QuestComponentKind.Ready:
                    //    Owner.Quests.Complete(TemplateId, 0);
                    //    continue;
                    case 0:
                        continue;
                }
                for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {
                    var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                    foreach (var act in acts)
                    {
                        switch (act.DetailType)
                        {
                            case "QuestActSupplyItem" when Step == QuestComponentKind.Supply:
                                {
                                    var next = Step;
                                    next++;
                                    var componentnext = Template.GetComponent(next);
                                    if (componentnext == null) break;
                                    var actsnext = QuestManager.Instance.GetActs(componentnext.Id);
                                    foreach (var qa in actsnext)
                                    {
                                        var questSupplyItem = (QuestActSupplyItem)QuestManager.Instance.GetActTemplate(act.DetailId, "QuestActSupplyItem");
                                        var questItemGather = (QuestActObjItemGather)QuestManager.Instance.GetActTemplate(qa.DetailId, "QuestActObjItemGather");
                                        switch (qa.DetailType)
                                        {
                                            case "QuestActObjItemGather" when questSupplyItem.ItemId == questItemGather.ItemId:
                                                res = act.Use(Owner, this, SupplyItem);
                                                ComponentId = components[componentIndex].Id;
                                                Owner.SendPacket(new SCQuestContextUpdatedPacket(this, ComponentId));
                                                break;
                                            default:
                                                res = false;
                                                break;
                                        }
                                    }
                                    break;
                                }
                            case "QuestActSupplyCopper" when Step == QuestComponentKind.Reward:
                                Owner.Quests.Complete(TemplateId, 0, false);
                                res = true;
                                return;
                            case "QuestActConReportNpc":
                            case "QuestActConAutoComplete":
                                res = false;
                                break;
                            default:
                                //case "QuestActObjMonsterGroupHunt":
                                //case "QuestActObjItemGather":
                                //case "QuestActObjItemUse":
                                //case "QuestActObjInteraction":
                                {
                                    res = act.Use(Owner, this, Objectives[componentIndex]);
                                    if (res)
                                        ComponentId = components[componentIndex].Id;
                                    Owner.SendPacket(new SCQuestContextUpdatedPacket(this, ComponentId));
                                    break;
                                }
                        }
                        SupplyItem = 0;
                        _log.Warn("Quest - {0}: ComponentId {1}, Step {2}, Status {3}, res {4}, act.DetailType {5}", TemplateId, ComponentId, Step, Status, res, act.DetailType); //  for debuging
                    }
                }
                if (!res)
                    break;

            }
        }

        public uint Complete(int selected)
        {
            var res = false;
            //ComponentId = 0u;
            for (; Step <= QuestComponentKind.Reward; Step++)
            {
                if (Step >= QuestComponentKind.Drop)
                    Status = QuestStatus.Completed;

                var components = Template.GetComponents(Step);
                if (components.Count == 0)
                    continue;

                for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {

                    var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                    var selective = 0;
                    foreach (var act in acts)
                    {
                        switch (act.DetailType)
                        {
                            case "QuestActSupplySelectiveItem":
                                {
                                    selective++;
                                    if (selective == selected)
                                        res = act.Use(Owner, this, Objectives[componentIndex]);

                                    break;
                                }
                            case "QuestActSupplyItem":
                                res = act.Use(Owner, this, SupplyItem);
                                break;
                            case "QuestActConAutoComplete":
                            case "QuestActConReportNpc":
                                res = act.Use(Owner, this, Objectives[componentIndex]);
                                ComponentId = components[componentIndex].Id;
                                break;
                            default:
                                res = act.Use(Owner, this, Objectives[componentIndex]);
                                var cStep = Template.LetItDone;
                                if (cStep && res == false)
                                {
                                    EarlyCompletion = true;
                                    res = true;
                                }
                                break;
                        }
                        SupplyItem = 0;
                    }
                }
                if (!res)
                    return ComponentId;
            }
            return res ? ComponentId : 0;
        }

        public int GetCustomExp() { return GetCustomSupplies("exp"); }

        public int GetCustomCopper() { return GetCustomSupplies("copper"); }

        public int GetCustomSupplies(string supply)
        {
            var value = 0;
            var component = Template.GetComponent(QuestComponentKind.Reward);
            if (component == null)
                return 0;

            var acts = QuestManager.Instance.GetActs(component.Id);
            foreach (var act in acts)
            {
                switch (act.DetailType)
                {
                    case "QuestActSupplyExp" when supply == "exp":
                        {
                            var template = act.GetTemplate<QuestActSupplyExp>();
                            value = template.Exp;
                            break;
                        }
                    case "QuestActSupplyCoppers" when supply == "copper":
                        {
                            var template = act.GetTemplate<QuestActSupplyCopper>();
                            value = template.Amount;
                            break;
                        }
                    default:
                        value = 0;
                        break;
                }
            }
            return value;
        }

        public void RemoveQuestItems()
        {
            for (Step = QuestComponentKind.None; Step <= QuestComponentKind.Reward; Step++)
            {
                var component = Template.GetComponent(Step);
                if (component == null)
                    continue;

                var acts = QuestManager.Instance.GetActs(component.Id);
                foreach (var act in acts)
                {
                    //var items = new List<(Item, int)>();
                    if (act.DetailType == "QuestActSupplyItem" && Step == QuestComponentKind.Supply)
                    {
                        var template = act.GetTemplate<QuestActSupplyItem>();
                        if (template.DestroyWhenDrop)
                            Owner.Inventory.TakeoffBackpack(ItemTaskType.QuestRemoveSupplies);

                        Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, template.ItemId, template.Count, null);
                        //items.AddRange(Owner.Inventory.RemoveItem(template.ItemId, template.Count));
                    }
                    if (act.DetailType == "QuestActObjItemGather")
                    {
                        var template = act.GetTemplate<QuestActObjItemGather>();
                        if (template.DestroyWhenDrop)
                        {
                            Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, template.ItemId, template.Count, null);
                            //items.AddRange(Owner.Inventory.RemoveItem(template.ItemId, template.Count));
                        }
                    }
                    /*
                    var tasks = new List<ItemTask>();
                    foreach (var (item, count) in items)
                    {
                        if (item.Count == 0)
                        {
                            tasks.Add(new ItemRemove(item));
                        }
                        else
                        {
                            tasks.Add(new ItemCountUpdate(item, -count));
                        }
                    }
                    Owner.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.QuestRemoveSupplies, tasks, new List<ulong>()));
                    */
                }
            }
        }

        public void RemoveQuestItems(bool dropped = false)
        {
            for (Step = QuestComponentKind.None; Step <= QuestComponentKind.Reward; Step++)
            {
                var components = Template.GetComponents(Step);
                if (components.Count == 0)
                    continue;

                foreach (var component in components)
                {
                    var acts = QuestManager.Instance.GetActs(component.Id);
                    foreach (var act in acts)
                    {
                        var items = new List<(Item, int)>();
                        switch (act.DetailType)
                        {
                            case "QuestActSupplyItem" when Step == QuestComponentKind.Supply:
                                {
                                    var template = act.GetTemplate<QuestActSupplyItem>();
                                    if (Owner.Inventory.GetAllItemsByTemplate(null, template.ItemId, -1, out var foundItems, out _))
                                    {
                                        foreach (var foundItem in foundItems)
                                        {
                                            if (!dropped)
                                                if (foundItem.Template.LootQuestId > 0)
                                                    Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, foundItem.Count, null);
                                                else
                                                {
                                                    if (foundItems.Count > template.Count)
                                                        Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, template.Count, null);
                                                    else
                                                        Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, foundItems.Count, null);
                                                }
                                            else if (foundItem.Template.LootQuestId > 0)
                                                Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, foundItem.Count, null); //Only remove quest given items if the quest was dropped 
                                        }
                                    }
                                    break;
                                }
                            case "QuestActObjItemGather":
                                {
                                    var template = act.GetTemplate<QuestActObjItemGather>();
                                    if (template.DestroyWhenDrop)
                                    {
                                        if (Owner.Inventory.GetAllItemsByTemplate(null, template.ItemId, -1, out var foundItems, out _))
                                        {
                                            foreach (var foundItem in foundItems)
                                            {
                                                if (!dropped)
                                                    if (foundItem.Template.LootQuestId > 0)
                                                        Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, foundItem.Count, null);
                                                    else
                                                    {
                                                        if (foundItems.Count > template.Count)
                                                            Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, template.Count, null);
                                                        else
                                                            Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, foundItems.Count, null);
                                                    }
                                                else if (foundItem.Template.LootQuestId > 0)
                                                    Owner.Inventory.ConsumeItem(null, ItemTaskType.QuestRemoveSupplies, foundItem.TemplateId, foundItem.Count, null); //Only remove quest given items if the quest was dropped 
                                            }
                                        }
                                    }
                                    break;
                                }
                        }
                    }
                }
            }
        }

        public void Drop(bool update)
        {
            Status = QuestStatus.Dropped;
            Step = QuestComponentKind.Drop;
            for (var i = 0; i < ObjectiveCount; i++)
                Objectives[i] = 0;

            if (update)
                Owner.SendPacket(new SCQuestContextUpdatedPacket(this, 0));

            RemoveQuestItems();
        }

        public void OnKill(Npc npc)
        {
            var res = false;
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActObjMonsterHunt":
                            {
                                var template = act.GetTemplate<QuestActObjMonsterHunt>();
                                if (template.NpcId == npc.TemplateId)
                                {
                                    Objectives[componentIndex]++;
                                    res = true;
                                    Status = QuestStatus.Progress;
                                    if (Template.Score > 0)
                                    {
                                        if (Objectives[componentIndex] >= (Template.Score / template.Count)) // TODO check to overtime
                                        {
                                            Status = QuestStatus.Ready;
                                        }
                                    }
                                    else
                                    {
                                        if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                        {
                                            Status = QuestStatus.Ready;
                                        }
                                    }
                                }
                                break;
                            }
                        case "QuestActObjMonsterGroupHunt":
                            {
                                var template = act.GetTemplate<QuestActObjMonsterGroupHunt>();
                                if (QuestManager.Instance.CheckGroupNpc(template.QuestMonsterGroupId, npc.TemplateId))
                                {
                                    Objectives[componentIndex]++;
                                    res = true;
                                    Status = QuestStatus.Progress;
                                    if (Template.Score > 0)
                                    {
                                        if (Objectives[componentIndex] >= (Template.Score / template.Count)) // TODO check to overtime
                                        {
                                            Status = QuestStatus.Ready;
                                        }
                                    }
                                    else
                                    {
                                        if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                        {
                                            Status = QuestStatus.Ready;
                                        }
                                    }
                                }
                                break;
                            }
                        default:
                            break;
                    }
                }
            }
            Update(res);
        }

        public void OnItemGather(Item item, int count)
        {
            var res = false;
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActObjMonsterGroupHunt":
                            {
                                res = false;
                                break;
                            }
                        case "QuestActSupplyItem":
                            {
                                var template = act.GetTemplate<QuestActSupplyItem>();
                                if (template.ItemId == item.TemplateId)
                                {
                                    res = true;
                                    SupplyItem += count;
                                    if (SupplyItem >= template.Count) // TODO check to overtime
                                    {
                                        SupplyItem = template.Count;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                        case "QuestActObjItemGather":
                            {
                                var template = act.GetTemplate<QuestActObjItemGather>();
                                if (template.ItemId == item.TemplateId)
                                {
                                    res = true;
                                    Objectives[componentIndex] += count;
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        Objectives[componentIndex] = template.Count;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                        case "QuestActObjItemGroupGather":
                            {
                                var template = act.GetTemplate<QuestActObjItemGroupGather>();
                                if (QuestManager.Instance.CheckGroupItem(template.ItemGroupId, item.TemplateId))
                                {
                                    res = true;
                                    Objectives[componentIndex] += count;
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        Objectives[componentIndex] = template.Count;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                    }
                }
            }
            Update(res);
        }

        public void OnItemUse(Item item)
        {
            var res = false;
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActObjItemUse":
                            {
                                var template = act.GetTemplate<QuestActObjItemUse>();
                                if (template.ItemId == item.TemplateId)
                                {
                                    res = true;
                                    Objectives[componentIndex]++;
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        Objectives[componentIndex] = template.Count;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                        case "QuestActObjItemGroupUse":
                            {
                                var template = act.GetTemplate<QuestActObjItemGroupUse>();
                                if (QuestManager.Instance.CheckGroupItem(template.ItemGroupId, item.TemplateId))
                                {
                                    res = true;
                                    Objectives[componentIndex]++;
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        Objectives[componentIndex] = template.Count;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                    }
                }
            }
            Update(res);
        }

        public void OnInteraction(WorldInteractionType type, Units.BaseUnit target)
        {
            var res = false;
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActObjInteraction":
                            {
                                var template = act.GetTemplate<QuestActObjInteraction>();
                                if (template.WorldInteractionId == type)
                                {
                                    var interactionTarget = (Doodad)target;
                                    if (template.DoodadId == interactionTarget.TemplateId)
                                    {
                                        ObjId = interactionTarget.ObjId;
                                        res = true;
                                        Objectives[componentIndex]++;
                                        if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                        {
                                            Objectives[componentIndex] = template.Count;
                                            Status = QuestStatus.Ready;
                                        }
                                    }
                                }
                                break;
                            }
                        case "QuestActObjItemGather":
                            {
                                var template = act.GetTemplate<QuestActObjItemGather>();
                                Objectives[componentIndex] = Owner.Inventory.GetItemsCount(template.ItemId);
                                res = false;
                                Status = QuestStatus.Progress;
                                if (target != null)
                                {
                                    var interactionTarget = (Doodad)target;
                                    ObjId = interactionTarget.ObjId;
                                }
                                if (Template.Score > 0)
                                {
                                    if (Objectives[componentIndex] >= (Template.Score / template.Count)) // TODO check to overtime
                                    {
                                        res = true;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                else
                                {
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        res = true;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                        case "QuestActObjItemUse":
                            {
                                var template = act.GetTemplate<QuestActObjItemUse>();
                                Objectives[componentIndex] = Owner.Inventory.GetItemsCount(template.ItemId);
                                res = false;
                                Status = QuestStatus.Progress;
                                if (target != null)
                                {
                                    var interactionTarget = (Doodad)target;
                                    ObjId = interactionTarget.ObjId;
                                }
                                if (Template.Score > 0)
                                {
                                    if (Objectives[componentIndex] >= (Template.Score / template.Count)) // TODO check to overtime
                                    {
                                        res = true;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                else
                                {
                                    if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                    {
                                        res = true;
                                        Status = QuestStatus.Ready;
                                    }
                                }
                                break;
                            }
                        default:
                            res = false;
                            break;
                    }
                }
            }
            Update(res);
        }

        public void OnLevelUp()
        {
            var res = false;
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActObjLevel":
                            {
                                var template = act.GetTemplate<QuestActObjLevel>();
                                if (template.Level >= Owner.Level)
                                {
                                    res = false;
                                    break;
                                }
                                else
                                {
                                    res = true;
                                    Objectives[componentIndex]++;
                                    break;
                                }
                            }
                        default:
                            //case not "QuestActObjLevel":
                            res = false;
                            break;
                    }
                }
            }
            Update(res);
        }

        public void OnQuestComplete(uint questContextId)
        {
            var res = false;
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActObjCompleteQuest":
                            {
                                var template = act.GetTemplate<QuestActObjCompleteQuest>();
                                if (template.QuestId == questContextId)
                                {
                                    res = true;
                                    Objectives[componentIndex]++;
                                }
                                break;
                            }
                    }
                }
            }
            Update(res);
        }

        public void RecalcObjectives(bool send = true)
        {
            var components = Template.GetComponents(Step);
            if (components.Count == 0)
                return;

            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var acts = QuestManager.Instance.GetActs(components[componentIndex].Id);
                foreach (var act in acts)
                {
                    switch (act.DetailType)
                    {
                        case "QuestActSupplyItem":
                            {
                                var template = act.GetTemplate<QuestActSupplyItem>();
                                Objectives[componentIndex] = Owner.Inventory.GetItemsCount(template.ItemId);
                                if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                {
                                    Objectives[componentIndex] = template.Count;
                                }
                                break;
                            }
                        case "QuestActObjItemGather":
                            {
                                var template = act.GetTemplate<QuestActObjItemGather>();
                                Objectives[componentIndex] = Owner.Inventory.GetItemsCount(template.ItemId);
                                if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                {
                                    Objectives[componentIndex] = template.Count;
                                }
                                break;
                            }
                        case "QuestActObjItemGroupGather":
                            {
                                var template = act.GetTemplate<QuestActObjItemGroupGather>();
                                Objectives[componentIndex] = 0;
                                foreach (var itemId in QuestManager.Instance.GetGroupItems(template.ItemGroupId))
                                {
                                    Objectives[componentIndex] += Owner.Inventory.GetItemsCount(itemId);
                                }
                                if (Objectives[componentIndex] >= template.Count) // TODO check to overtime
                                {
                                    Objectives[componentIndex] = template.Count;
                                }
                                break;
                            }
                    }
                }
            }
            Update(send);
        }

        public void ClearObjectives()
        {
            Objectives = new int[ObjectiveCount];
        }

        public int[] GetObjectives(QuestComponentKind step)
        {
            return Objectives;
        }

        private int GetScore(List<QuestComponent> components)
        {
            var score = 0;
            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var component = components[componentIndex];
                var acts = QuestManager.Instance.GetActs(component.Id).Select(a => a.GetTemplate()).OfType<IQuestActScoreProvider>().ToList();
                score += acts.Sum(act => act.GetScoreForObjective(Objectives[componentIndex]));
            }
            return score;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(Id);
            stream.Write(TemplateId);
            stream.Write((byte)Status);
            foreach (var objective in Objectives) // TODO do-while, count 5
            {
                stream.Write(objective);
            }

            stream.Write(false);             // isCheckSet
            stream.WriteBc((uint)ObjId);     // ObjId
            stream.Write(0u);                // type(id)
            stream.WriteBc((uint)ObjId);     // ObjId
            stream.WriteBc((uint)ObjId);     // ObjId
            stream.Write(LeftTime);
            stream.Write(0u);                      // type(id)
            stream.Write(DoodadId);                // doodadId
            stream.Write(DateTime.UtcNow);         // acceptTime
            stream.Write((byte)QuestAcceptorType); // type QuestAcceptorType
            stream.Write(AcceptorType);            // acceptorType npcId or doodadId
            return stream;
        }

        public void ReadData(byte[] data)
        {
            var stream = new PacketStream(data);
            for (var i = 0; i < ObjectiveCount; i++)
            {
                Objectives[i] = stream.ReadInt32();
            }

            Step = (QuestComponentKind)stream.ReadByte();
            QuestAcceptorType = (QuestAcceptorType)stream.ReadByte();
            ComponentId = stream.ReadUInt32();
            AcceptorType = stream.ReadUInt32();
            Time = stream.ReadDateTime();
        }

        public byte[] WriteData()
        {
            var stream = new PacketStream();
            foreach (var objective in Objectives)
            {
                stream.Write(objective);
            }

            stream.Write((byte)Step);
            stream.Write((byte)QuestAcceptorType);
            stream.Write(ComponentId);
            stream.Write(AcceptorType);
            stream.Write(Time);
            return stream.GetBytes();
        }
    }
}

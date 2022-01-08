using System.Linq;

using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Static;

namespace AAEmu.Game.Utils
{
    public class QuestCommandUtil
    {
        public static void GetCommandChoice(Character character, string choice, string[] args)
        {
            uint questId;

            switch (choice)
            {
                case "add":
                    if (args.Length >= 2)
                    {
                        if (uint.TryParse(args[1], out questId))
                        {
                            character.Quests.Add(questId);
                        }
                    }
                    else
                    {
                        character.SendMessage("[Quest] Proper usage: /quest add <questId>");
                    }
                    break;
                case "list":
                    break;
                case "reward":
                    if (args.Length >= 2)
                    {
                        if (uint.TryParse(args[1], out questId))
                        {
                            if (args.Length >= 3 && int.TryParse(args[2], out var selectedId))
                            {
                                character.Quests.Complete(questId, selectedId);
                            }
                            else
                            {
                                character.Quests.Complete(questId, 0);
                            }
                        }
                    }
                    else
                    {
                        character.SendMessage("[Quest] Proper usage: /quest reward <questId>");
                    }
                    break;
                case "step":
                    if (args.Length >= 2)
                    {
                        if (uint.TryParse(args[1], out questId))
                        {
                            if (character.Quests.HasQuest(questId))
                            {
                                if (args.Length >= 3 && uint.TryParse(args[2], out var stepId))
                                {
                                    if(character.Quests.SetStep(questId, stepId))
                                        character.SendMessage("[Quest] set Step {0} for Quest {1}", stepId, questId);
                                    else
                                        character.SendMessage("[Quest] Proper usage: /quest step <questId> <stepId>");
                                }
                            }
                            else
                            {
                                character.SendMessage("[Quest] You do not have the quest {0}", questId);
                            }
                        }
                    }
                    else
                    {
                        character.SendMessage("[Quest] Proper usage: /quest step <questId> <stepId>");
                    }
                    break;
                case "prog":
                    if (args.Length >= 2)
                    {
                        if (uint.TryParse(args[1], out questId))
                        {
                            if (character.Quests.HasQuest(questId))
                            {
                                var quest = character.Quests.Quests[questId];
                                quest.Update();
                            }
                            else
                            {
                                character.SendMessage("[Quest] You do not have the quest {0}", questId);
                            }
                        }
                    }
                    else
                    {
                        character.SendMessage("[Quest] Proper usage: /quest prog <questId>");
                    }
                    break;
                case "remove":
                    if (args.Length >= 2)
                    {
                        if (uint.TryParse(args[1], out questId))
                        {
                            if (character.Quests.HasQuest(questId))
                            {
                                character.Quests.Drop(questId, true);
                            }
                            else
                            {
                                character.SendMessage("[Quest] You do not have the quest {0}", questId);
                            }
                        }
                    }
                    else
                    {
                        character.SendMessage("[Quest] Proper usage: /quest remove <questId>");
                    }
                    break;
                default:
                    character.SendMessage("[Quest] /quest <add/remove/list/prog/step/reward>");
                    break;
            }
        }
    }
}

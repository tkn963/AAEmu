using System;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects
{
    public class GiveHonorPoint : SpecialEffectAction
    {
        protected override SpecialType SpecialEffectActionType => SpecialType.GiveHonorPoint;
        
        public override void Execute(Unit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj, CastAction castObj,
            Skill skill, SkillObject skillObject, DateTime time, int amount, int value2, int value3, int value4)
        {
            if (!(caster is Character character))
                return;

            character.HonorPoint += (int)Math.Round(AppConfiguration.Instance.World.HonorRate * amount);
            character.SendPacket(new SCGamePointChangedPacket(0, amount));
        }
    }
}

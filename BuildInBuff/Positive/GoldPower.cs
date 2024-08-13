using System;
using BuiltinBuffs.Positive;
using MonoMod;
using RandomBuff;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TemplateGains
{
    //��Ǯ��������
    class GoldPowerBuff : Buff<GoldPowerBuff, GoldPowerBuffData> { public override BuffID ID => GoldPowerBuffEntry.GoldPowerID; }
    class GoldPowerBuffData : BuffData { public override BuffID ID => GoldPowerBuffEntry.GoldPowerID; }
    class GoldPowerBuffEntry : IBuffEntry
    {
        public static BuffID GoldPowerID = new BuffID("GoldPowerID", true);
        public void OnEnable()
        {
            BuffRegister.RegisterBuff<GoldPowerBuff, GoldPowerBuffData, GoldPowerBuffEntry>(GoldPowerID);
        }
        public static void HookOn()
        {
            On.Player.Jump += Player_Jump;
            On.Player.ThrownSpear += Player_ThrownSpear;
        }

        private static void Player_ThrownSpear(On.Player.orig_ThrownSpear orig, Player self, Spear spear)
        {
            orig.Invoke(self, spear);
            //GoldPowerBuff.Instance.TriggerSelf(true);
            spear.spearDamageBonus += 0.6f * GoldPower(self);
            spear.firstChunk.vel *= 1 + 0.3f * GoldPower(self);
        }

        private static void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            orig.Invoke(self);
            self.jumpBoost += 1.5f * GoldPower(self);

        }
        public static int GoldPower(Player self)
        {
            int power = 0;
            //������ϵ���������
            for (int i = 0; i < self.grasps.Length; i++)
            {
                if (self.grasps[i] != null && self.grasps[i].grabbed != null && self.grasps[i].grabbed is DataPearl) power++;
            }
            //�����������������
            if (self.objectInStomach != null && self.objectInStomach is DataPearl.AbstractDataPearl) power++;

            //��ⳬ��θ�ڵ���������
            if(SuperStomachBuffEntry.SuperStomach.GetBuffData()!=null)
            {
                if (SuperStomachBuffEntry.bigStomach.TryGetValue(self, out var stomach))
                {
                    for (int i = 0; i < stomach.objectsInStomach.Count; i++)
                    {
                        if (stomach.objectsInStomach[i].type == AbstractPhysicalObject.AbstractObjectType.DataPearl) power++;
                    }

                }
            }


            return power;
        }

    }
}

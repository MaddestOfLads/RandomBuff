using BuiltinBuffs.Positive;
using HotDogGains.Duality;
using RandomBuff;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace BuildInBuff.Positive
{
    class DreamtOfABatBuff : Buff<DreamtOfABatBuff, DreamtOfABatBuffData> { public override BuffID ID => DreamtOfABatBuffEntry.DreamtOfABatID; }
    class DreamtOfABatBuffData : BuffData
    {
        public override BuffID ID => DreamtOfABatBuffEntry.DreamtOfABatID;
        public override bool CanStackMore() => StackLayer < 2;
    }
    class DreamtOfABatBuffEntry : IBuffEntry
    {
        public static BuffID DreamtOfABatID = new BuffID("DreamtOfABatID", true);
        public void OnEnable()
        {
            BuffRegister.RegisterBuff<DreamtOfABatBuff, DreamtOfABatBuffData, DreamtOfABatBuffEntry>(DreamtOfABatID);
        }
        public static void HookOn()
        {
            //��ѣ��ʱ��������
            On.Player.Stun += Player_Stun;
            //��ֹ��ʧʱ��������
            On.Player.Die += Player_Die;

            //�ı�����������ɫ
            On.FlyGraphics.ApplyPalette += ButteFly_ApplyPalette;

        }

        private static void ButteFly_ApplyPalette(On.FlyGraphics.orig_ApplyPalette orig, FlyGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig.Invoke(self, sLeaser, rCam, palette);

            //�������������ɫ����ҵ���ɫһ��
            if (ButteFly.modules.TryGetValue(self.fly, out var butteFly))
            {
                for (int i = 0; i < 3; i++)
                {
                    sLeaser.sprites[i].color = butteFly.color;
                }
            }

        }


        private static void Player_Die(On.Player.orig_Die orig, Player self)
        {
            if (self.stun > 0 && self.slatedForDeletetion)
            {
                foreach (var item in self.room.updateList)
                {
                    if (item is BatBody body && body.player == self)
                    {
                        return;
                    }
                }
            }
            orig.Invoke(self);
        }

        private static void Player_Stun(On.Player.orig_Stun orig, Player self, int st)
        {
            orig.Invoke(self, st);

            if (self.dead) return;

            foreach (var item in self.room.updateList)
            {
                if (item is BatBody body && body.player == self)
                {
                    return;
                }
            }

            //��΢���һ����ֵ��ֹĪ������ķ�������
            if (st > 5) self.room.AddObject(new BatBody(self));

        }
    }

    public class BatBody : UpdatableAndDeletable
    {

        public Player player;

        public Fly batBody;

        public BatBody(Player player)
        {
            DreamtOfABatBuff.Instance.TriggerSelf(true);

            this.player = player;


            //�ٻ���ɫ����
            var room = player.room;
            batBody = new Fly(new AbstractCreature(room.world, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Fly), null, room.GetWorldCoordinate(player.DangerPos), room.world.game.GetNewID()), room.world);
            ButteFly.modules.Add(batBody, new ButteFly(player.ShortCutColor()));
            //�ƶ�����
            batBody.PlaceInRoom(room);
            batBody.firstChunk.HardSetPosition(player.firstChunk.pos);
            batBody.firstChunk.vel += player.firstChunk.vel;

            //��Ч
            SpawnSparck(room);

            player.wantToPickUp = 0;

            //�÷����Զ�ɾ�����
            player.slatedForDeletetion = true;
        }

        public override void Destroy()
        {
            if (player.slatedForDeletetion && !batBody.slatedForDeletetion)
            {
                player.slatedForDeletetion = false;

                //��ֹ�ظ�������
                bool notHavePlayer = true;
                //�������Ƿ��Ѿ�������
                foreach (var item in room.abstractRoom.creatures)
                {
                    if (item == player.abstractCreature) notHavePlayer = false;
                }

                //�������
                if (notHavePlayer)
                {

                    //������û�оʹ���һ�����
                    room.abstractRoom.AddEntity(player.abstractCreature);
                    player.PlaceInRoom(room);

                    //����ҵ�����λ��
                    for (int i = 0; i < player.bodyChunks.Length; i++)
                    {
                        player.bodyChunks[i].HardSetPosition(batBody.firstChunk.pos);

                        player.bodyChunks[i].vel = batBody.firstChunk.vel;
                    }
                    //�������վ��
                    player.standing = true;

                    player.graphicsModule.Reset();
                }

            }
            if (batBody.dead || batBody.slatedForDeletetion) player.Die();

            batBody.Destroy();
            base.Destroy();

        }

        public void SpawnSparck(Room room)
        {
            room.AddObject(new Explosion.ExplosionLight(player.firstChunk.pos, 80, 1, 20, Custom.hexToColor("93c5d4")));
        }
        public override void Update(bool eu)
        {
            base.Update(eu);
            if (DreamtOfABatBuffEntry.DreamtOfABatID.GetBuffData().StackLayer > 1)
            {
                batBody.firstChunk.vel += RWInput.PlayerInput(player.playerState.playerNumber).analogueDir;
            }


            batBody.enteringShortCut = null;
            batBody.shortcutDelay = 40;

            player.mainBodyChunk.pos = batBody.firstChunk.pos;

            if (batBody.slatedForDeletetion) player.stun = 0;

            if (player.stun <= 0) this.Destroy();
            else player.stun--;
        }


    }

    public class ButteFly
    {
        public static ConditionalWeakTable<Fly, ButteFly> modules = new ConditionalWeakTable<Fly, ButteFly>();

        public Color color;
        public ButteFly(Color color) { this.color = color; }
    }
}
﻿using RandomBuff;
using RandomBuff.Core.Buff;
using RandomBuff.Core.Entry;
using RandomBuffUtils;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static RandomBuffUtils.PlayerUtils;
using Random = UnityEngine.Random;

namespace BuiltinBuffs.Positive
{
    internal class ShockingSpeedBuff : Buff<ShockingSpeedBuff, ShockingSpeedBuffData>, IOWnPlayerUtilsPart
    {
        public override BuffID ID => ShockingSpeedBuffEntry.shockingSpeedBuffID;

        public ShockingSpeedBuff()
        {
            PlayerUtils.AddPart(this);
        }

        public override void Destroy()
        {
            base.Destroy();
            PlayerUtils.RemovePart(this);
        }

        public PlayerModuleGraphicPart InitGraphicPart(PlayerModule module)
        {
            return new ShockingSpeedGraphicModule();
        }

        public PlayerModulePart InitPart(PlayerModule module)
        {
            return new ShockingSpeedModule();
        }

        internal class ShockingSpeedGraphicModule : PlayerModuleGraphicPart
        {
            static Color electricCol = Helper.GetRGBColor(53, 54, 255);
            int savPoss = 10;
            public List<Vector2> positionsList = new List<Vector2>();
            public List<Color> colorsList = new List<Color>();

            public bool shocked;
            float totAlpha;

            public override void InitSprites(SLeaserInstance sLeaserInstance, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
            {
                sLeaserInstance.sprites = new FSprite[1];
                sLeaserInstance.sprites[0] = TriangleMesh.MakeLongMesh(this.savPoss - 1, false, true);
            }

            public override void AddToContainer(SLeaserInstance sLeaserInstance, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
            {
                rCam.ReturnFContainer("Water").AddChild(sLeaserInstance.sprites[0]);
            }

            public override void DrawSprites(SLeaserInstance sLeaserInstance, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
            {
                Vector2 vector = Vector2.Lerp(self.player.firstChunk.lastPos, self.player.firstChunk.pos, timeStacker);
                float size = 1f;
                for (int i = 0; i < this.savPoss - 1; i++)
                {
                    Vector2 smoothPos = this.GetSmoothPos(i, timeStacker);
                    Vector2 smoothPos2 = this.GetSmoothPos(i + 1, timeStacker);
                    Vector2 vector2 = (vector - smoothPos).normalized;
                    Vector2 vector3 = Custom.PerpendicularVector(vector2);
                    vector2 *= Vector2.Distance(vector, smoothPos2) / 5f;
                    (sLeaserInstance.sprites[0] as TriangleMesh).MoveVertice(i * 4, vector - vector3 * size - vector2 - camPos);
                    (sLeaserInstance.sprites[0] as TriangleMesh).MoveVertice(i * 4 + 1, vector + vector3 * size - vector2 - camPos);
                    (sLeaserInstance.sprites[0] as TriangleMesh).MoveVertice(i * 4 + 2, smoothPos - vector3 * size + vector2 - camPos);
                    (sLeaserInstance.sprites[0] as TriangleMesh).MoveVertice(i * 4 + 3, smoothPos + vector3 * size + vector2 - camPos);
                    vector = smoothPos;
                }
                for (int j = 0; j < (sLeaserInstance.sprites[0] as TriangleMesh).verticeColors.Length; j++)
                {
                    float num2 = (float)j / (float)((sLeaserInstance.sprites[0] as TriangleMesh).verticeColors.Length - 1);
                    Color col = GetCol(j);
                    col.a = num2 * totAlpha;
                    (sLeaserInstance.sprites[0] as TriangleMesh).verticeColors[j] = col;
                }
            }

            public override void Update(PlayerGraphics playerGraphics)
            {
                positionsList.Insert(0, playerGraphics.player.DangerPos + Custom.RNV() * Random.Range(0f, 20f));
                if (positionsList.Count > 10)
                {
                    positionsList.RemoveAt(10);
                }
                colorsList.Insert(0, new Color(electricCol.r + Random.value * 0.1f, electricCol.g + Random.value * 0.1f, electricCol.b));
                if (colorsList.Count > 10)
                {
                    colorsList.RemoveAt(10);
                }

                if (shocked)
                {
                    totAlpha = 1f;
                }
                else if(totAlpha > 0)
                {
                    totAlpha = Mathf.Lerp(totAlpha, 0f, 0.25f);
                    if (Mathf.Approximately(totAlpha, 0f))
                        totAlpha = 0f;
                }
            }

            Color GetCol(int i)
            {
                return colorsList[Custom.IntClamp(i, 0, colorsList.Count - 1)];
            }

            Vector2 GetPos(int i)
            {
                return positionsList[Custom.IntClamp(i, 0, positionsList.Count - 1)];
            }

            private Vector2 GetSmoothPos(int i, float timeStacker)
            {
                return Vector2.Lerp(GetPos(i + 1), GetPos(i), timeStacker);
            }


            public void Reset(PlayerGraphics self)
            {
                positionsList = new List<Vector2> { self.player.DangerPos, self.player.DangerPos };
                colorsList = new List<Color> { electricCol };
            }
        }

        internal class ShockingSpeedModule : PlayerModulePart
        {
            public int counter;
            float origSpeedFactor;

            public override void Update(Player player, bool eu)
            {
                base.Update(player, eu);
                if(counter > 0)
                {
                    counter--;
                    if (counter == 0)
                    {
                        player.slugcatStats.runspeedFac = origSpeedFactor;
                        if (PlayerUtils.TryGetGraphicPart<ShockingSpeedGraphicModule>(player, ShockingSpeedBuff.Instance, out var module))
                        {
                            module.shocked = false;
                        }
                    }
                }    
            }

            public void GetShocked(Player player)
            {
                if(counter == 0)
                {
                    origSpeedFactor = player.slugcatStats.runspeedFac;
                    player.slugcatStats.runspeedFac *= 3f;

                    if (PlayerUtils.TryGetGraphicPart<ShockingSpeedGraphicModule>(player, ShockingSpeedBuff.Instance, out var module))
                    {
                        module.Reset(player.graphicsModule as PlayerGraphics);
                        module.shocked = true;
                    }
                }
                counter = 160;
            }
        }
    }

    internal class ShockingSpeedBuffData : BuffData
    {
        public override BuffID ID => ShockingSpeedBuffEntry.shockingSpeedBuffID;
    }

    internal class ShockingSpeedBuffEntry : IBuffEntry
    {
        public static BuffID shockingSpeedBuffID = new BuffID("ShockingSpeed", true);

        public void OnEnable()
        {
            BuffRegister.RegisterBuff<ShockingSpeedBuff, ShockingSpeedBuffData, ShockingSpeedBuffEntry>(shockingSpeedBuffID);
        }

        public static void HookOn()
        {
            On.Creature.Violence += Creature_Violence;
        }

        private static void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, UnityEngine.Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
        {
            orig.Invoke(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
            if(self is Player player && type == Creature.DamageType.Electric)
            {
                if(PlayerUtils.TryGetModulePart<ShockingSpeedBuff.ShockingSpeedModule>(player, ShockingSpeedBuff.Instance, out var part))
                {
                    part.GetShocked(player);
                }
            }
        }
    }
}

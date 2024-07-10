﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RandomBuff.Core.Game;
using RandomBuff.Core.Progression.Quest.Condition;
using RandomBuff.Core.SaveData;
using RWCustom;
using UnityEngine;
using static Expedition.ExpeditionProgression;

namespace RandomBuff.Core.Progression
{

    public class QuestUnlockedType : ExtEnum<QuestUnlockedType>
    {
        public QuestUnlockedType(string value, bool register = false) : base(value,register)
        {
        }

        public static void Init()
        {
            _ = Card;
            _ = Mission;
            _ = Cosmetic;
            _ = Special;

        }

        public static readonly QuestUnlockedType Card = new(nameof(Card), true);
        public static readonly QuestUnlockedType Mission = new(nameof(Mission), true);
        public static readonly QuestUnlockedType Cosmetic = new(nameof(Cosmetic), true);
        public static readonly QuestUnlockedType Special = new(nameof(Special), true);

    }



    /// <summary>
    /// 储存Quest的信息，并负责实际判断，
    /// 但是不存存档变化，不储存数据（数据请到BuffPlayerData）
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BuffQuest
    {
      
        /// <summary>
        /// 任务名称
        /// </summary>
        public string QuestName => questName;

        /// <summary>
        /// 任务Id
        /// </summary>
        public string QuestId => id;

        /// <summary>
        /// 任务颜色
        /// </summary>
        public Color QuestColor => color;

        /// <summary>
        /// 已解锁物品
        /// </summary>
        public Dictionary<QuestUnlockedType, string[]> UnlockItem => unlockItem;


        /// <summary>
        /// 解锁条件
        /// 目前只存在与关联，没有或关联
        /// </summary>
        public List<QuestCondition> QuestConditions = new();


        /// <summary>
        /// 在结算时调用，返回更新后的任务解锁状态
        /// </summary>
        /// <returns></returns>
        public bool UpdateUnlockedState(WinGamePackage package)
        {
            return QuestConditions.All(i => i.UpdateUnlockedState(package));
        }

        /// <summary>
        /// 在初始化时调用，验证数据是否有效
        /// </summary>
        /// <returns></returns>
        public bool VerifyData()
        {
            return QuestConditions.Count != 0 && QuestConditions.All(i => i.VerifyData());
        }


        [JsonProperty("Name")] 
        private string questName;

        [JsonProperty("Color")]
        private string ColorString
        {
            set
            {
                try
                {
                    color = Custom.hexToColor(value);
                }
                catch (Exception _)
                {
                    BuffPlugin.LogError($"BuffQuest: Invalid color value: {value}, At BuffQuest named: {questName}");
                    color = Color.white;
                }
            }
        }

        [JsonProperty("ID")] 
        private string id;

        [JsonProperty("UnlockItem")]
        private Dictionary<string, string[]> RawUnlockItem
        {
            set
            {
                unlockItem = new Dictionary<QuestUnlockedType, string[]>();
                foreach (var item in value)
                {
                    unlockItem.Add(new QuestUnlockedType(item.Key),item.Value);
                }
            }
        }


        [JsonProperty("Conditions")]
        private List<JObject> Conditions
        {
            set
            {
                QuestConditions = new();
                foreach (var raw in value)
                {
                    var rawValue = raw.ToString();
                    string typeName = null;
                    try
                    {
                        typeName = JsonConvert.DeserializeObject<QuestConditionJsonGetType>(rawValue).TypeName;
                    }
                    catch (Exception e)
                    {
                        BuffPlugin.LogException(e, $"Read Condition for Quest:{QuestId},TypeName Error at:{raw}!");
                        continue;
                    }

                    if (typeName == null)
                    {
                        BuffPlugin.LogError($"Read Condition, Quest:{QuestId}, Null Typename at:{raw}");
                        continue;
                    }

                    if (QuestCondition.TryGetType(typeName, out var type))
                    {
                        try
                        {
                            var conditions = (QuestCondition)JsonConvert.DeserializeObject(rawValue, type);
                            QuestConditions.Add(conditions);
                        }
                        catch (Exception e)
                        {
                            BuffPlugin.LogException(e, $"Read Condition for Quest:{QuestId}, Condition Type:{typeName} Error!");
                        }
                        
                    }
                    else
                    {
                        BuffPlugin.LogError($"Unknown QuestCondition Type!  Quest:{QuestId}, Type:{typeName}");
                    }
                }
            }
        }

        private Dictionary<QuestUnlockedType, string[]> unlockItem;

        private Color color;

    }


}
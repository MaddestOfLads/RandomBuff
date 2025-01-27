﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JollyCoop;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using RandomBuff.Cardpedia;
using RandomBuff.Core.Buff;
using RandomBuff.Core.BuffMenu;
using RandomBuff.Core.BuffMenu.Test;
using RandomBuff.Core.Entry;
using RandomBuff.Core.GachaMenu;
using RandomBuff.Core.Game;
using RandomBuff.Core.SaveData;
using RandomBuff.Core.StaticsScreen;
using RandomBuff.Credit;
using RWCustom;
using UnityEngine;
using static RandomBuff.Core.BuffMenu.BuffGameMenu;

namespace RandomBuff.Core.Hooks
{
    static  partial class CoreHooks
    {
        public static void OnModsInit()
        {
            On.ProcessManager.PreSwitchMainProcess += ProcessManager_PreSwitchMainProcess;
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;


            On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
            On.PlayerProgression.WipeAll += PlayerProgression_WipeAll;

            On.Menu.MainMenu.Singal += MainMenu_Singal;

            On.Menu.MainMenu.ctor += (orig, self, manager, bkg) =>
            {
                orig(self, manager, bkg);
                var button = self.mainMenuButtons.First(i => i.signalText == "BUFF");
                var infoButton = new IconButton(self, self.pages[0], "buffassets/illustrations/RandomBuff_Cardpedia", "CARDPEDIA_ICON",
                    button.pos + Vector2.right * (button.size.x+5), new Vector2(30, 30), 0.6f);
                CardpediaMenuHooks.menu = self;
                self.pages[0].subObjects.Add(infoButton);
            };

            IL.Menu.MainMenu.ctor += (il) =>
            {
                InsertMainMenuButtonAfter(il, "STORY", self =>
                {
                    float buttonWidth = MainMenu.GetButtonWidth(self.CurrLang);
                    Vector2 pos = new Vector2(683f - buttonWidth / 2f, 0f);
                    Vector2 size = new Vector2(buttonWidth, 30f);
                    
                    self.AddMainMenuButton(new SimpleButton(self, self.pages[0], BuffResourceString.Get("MainMenu_Buff"), "BUFF", pos,
                        size)
                        , () =>
                        {
                            self.manager.RequestMainProcessSwitch(BuffEnums.ProcessID.BuffGameMenu);
                            self.PlaySound(SoundID.MENU_Switch_Page_In);
                        }, 0);

                });
            };

            IL.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess1;


            IL.Menu.MainMenu.AddMainMenuButton += (il) =>
            {
                if (il.Body.Variables.Count >= 2 &&
                    il.Body.Variables[1].VariableType == il.Module.TypeSystem.Int32)
                {
                    ILCursor c = new ILCursor(il);
                    while (c.TryGotoNext(MoveType.After, i => i.MatchStloc(1)))
                    {
                        c.Emit(OpCodes.Ldloc_1);
                        c.EmitDelegate<Func<int, int>>(orig => Mathf.Min(11, orig + 2));
                        c.Emit(OpCodes.Stloc_1);
                    }
                }
                else
                {
                    BuffPlugin.LogError("Hook MainMenu AddMainMenuButton Failed");
                }
            };

            On.Menu.SlugcatSelectMenu.SlugcatUnlocked += SlugcatSelectMenu_SlugcatUnlocked;
            On.Menu.SlugcatSelectMenu.SlugcatPage.Scroll += SlugcatPage_Scroll;
            On.Menu.SlugcatSelectMenu.SlugcatPage.NextScroll += SlugcatPage_NextScroll;


            On.SlugcatStats.SlugcatUnlocked += SlugcatStats_SlugcatUnlocked;
            On.JollyCoop.JollyCustom.SlugClassMenu += JollyCustom_SlugClassMenu;
            On.JollyCoop.JollyMenu.JollyPlayerSelector.Update += JollyPlayerSelector_Update;
            On.ModManager.ModFolderHasDLLContent += ModManager_ModFolderHasDLLContent;

            On.PlayerProgression.CopySaveFile += PlayerProgression_CopySaveFile;
            On.Menu.BackupManager.RestoreSaveFile += BackupManager_RestoreSaveFile;
            On.Options.GetSaveFileName_SavOrExp += Options_GetSaveFileName_SavOrExp;

            InGameHooksInit();


            BuffPlugin.Log("Core Hook Loaded");

            //BuffGameMenu = new("BuffGameMenu");

        }

        private static void ProcessManager_PostSwitchMainProcess1(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            ILLabel label = c.DefineLabel();
            try
            {
                c.GotoNext(
                    i => i.MatchLdarg(0),
                    i => i.MatchLdfld<ProcessManager>("rainWorld"),
                    i => i.MatchLdfld<RainWorld>("progression"),
                    i => i.MatchCallvirt<PlayerProgression>("Revert"));
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<ProcessManager.ProcessID,bool>>((id) => id == BuffEnums.ProcessID.StackMenu || id == BuffEnums.ProcessID.UnstackMenu);
                c.Emit(OpCodes.Brtrue, label);
                c.GotoNext(MoveType.After, i => i.MatchCallvirt<PlayerProgression>("Revert"));
                c.MarkLabel(label);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }



        #region SaveSlot

        private static string Options_GetSaveFileName_SavOrExp(On.Options.orig_GetSaveFileName_SavOrExp orig, Options self)
        {
            var re = orig(self);
            if (self.saveSlot >= 100)
                return $"buffMain{self.saveSlot}";
            return re;
        }

        private static void BackupManager_RestoreSaveFile(On.Menu.BackupManager.orig_RestoreSaveFile orig, BackupManager self, string sourceName)
        {
            orig(self, sourceName);
            if (sourceName.StartsWith("sav"))
            {
                sourceName = sourceName.Replace("sav", "");
                if (string.IsNullOrEmpty(sourceName)) sourceName = "1";
                if (int.TryParse(sourceName, out var result))
                {
                    orig(self, $"buffMain{result + 99}");
                    orig(self, $"buffsave{result + 99}");
                    BuffPlugin.Log($"restored buff sav at slot:{result}");
                    BuffFile.BackUpForceUpdate = true;
                }
                else
                {
                    BuffPlugin.LogWarning($"unknown file name:sav{sourceName}");
                }
            }
        }

        private static void PlayerProgression_CopySaveFile(On.PlayerProgression.orig_CopySaveFile orig, PlayerProgression self, string sourceName, string destinationDirectory)
        {
            if (File.Exists(Path.Combine(destinationDirectory, sourceName)))
                return;
            orig(self, sourceName, destinationDirectory);
            if (sourceName.StartsWith("sav"))
            {
                sourceName = sourceName.Replace("sav", "");
                if (string.IsNullOrEmpty(sourceName)) sourceName = "1";
                if (int.TryParse(sourceName, out var result))
                {
                    if (!File.Exists(Path.Combine(destinationDirectory, $"buffMain{result + 99}")))
                        orig(self, $"buffMain{result + 99}", destinationDirectory);

                    if (!File.Exists(Path.Combine(destinationDirectory, $"buffsave{result + 99}")))
                        orig(self, $"buffsave{result + 99}", destinationDirectory);
                    BuffPlugin.Log($"backup buff sav at slot:{result}, folder:{destinationDirectory}");
                }
                else
                {
                    BuffPlugin.LogWarning($"unknown file name:sav{sourceName}");
                }
            }
        }

        private static void PlayerProgression_WipeAll(On.PlayerProgression.orig_WipeAll orig, PlayerProgression self)
        {
            orig(self);
            BuffDataManager.Instance.DeleteAll();
            BuffPlayerData.LoadBuffPlayerData("", BuffPlugin.saveVersion);
            BuffConfigManager.LoadConfig("", BuffPlugin.saveVersion);
            BuffFile.Instance.DeleteAllFile();

        }

        private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber)
        {
            orig(self, saveStateNumber);
            if (self.rainWorld.BuffMode())
                BuffDataManager.Instance.DeleteSaveData(saveStateNumber);
        }
        #endregion

        #region DllContent

        private static bool ModManager_ModFolderHasDLLContent(On.ModManager.orig_ModFolderHasDLLContent orig, string folder)
        {
            return orig(folder) || Directory.Exists(Path.Combine(folder, "buffplugins"));
        }

        #endregion

        #region MainMenu

        private static void MainMenu_Singal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
        {
            if (message == "CARDPEDIA_ICON")
            {
                CardpediaMenuHooks.CollectionButtonPressed();
            }
            orig(self, sender, message);
        }

        private static void InsertMainMenuButtonAfter(ILContext il, string beforeName,
            Action<MainMenu> createDeg)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(i => i.MatchLdstr(beforeName));
            c.GotoNext(MoveType.After, i => i.MatchCall<MainMenu>(nameof(MainMenu.AddMainMenuButton)));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate(createDeg.Invoke);
        }


        private static void JollyPlayerSelector_Update(On.JollyCoop.JollyMenu.JollyPlayerSelector.orig_Update orig, JollyCoop.JollyMenu.JollyPlayerSelector self)
        {
            orig(self);
            if (self.menu.manager.rainWorld.BuffMode() && self.index == 0)
                self.classButton.GetButtonBehavior.greyedOut = true;

        }



        private static bool SlugcatStats_SlugcatUnlocked(On.SlugcatStats.orig_SlugcatUnlocked orig, SlugcatStats.Name i, RainWorld rainWorld)
        {
            if (!Custom.rainWorld.BuffMode())
                return orig(i, rainWorld);
            return true;
        }

        private static SlugcatStats.Name JollyCustom_SlugClassMenu(On.JollyCoop.JollyCustom.orig_SlugClassMenu orig, int playerNumber, SlugcatStats.Name fallBack)
        {
            if (!Custom.rainWorld.BuffMode())
                return orig(playerNumber, fallBack);

            SlugcatStats.Name name = JollyCustom.JollyOptions(playerNumber).playerClass;
            if (name == null ||
                SlugcatStats.HiddenOrUnplayableSlugcat(name) ||
                (SlugcatStats.IsSlugcatFromMSC(name) && !ModManager.MSC))
            {
                JollyCustom.JollyOptions(playerNumber).playerClass = fallBack;
                name = fallBack;
            }
            return name;
        }

        private static float SlugcatPage_NextScroll(On.Menu.SlugcatSelectMenu.SlugcatPage.orig_NextScroll orig, SlugcatSelectMenu.SlugcatPage self, float timeStacker)
        {
            if (self is SlugcatIllustrationPage page)
                return page.NextScroll(timeStacker);
            return orig(self, timeStacker);
        }

        private static float SlugcatPage_Scroll(On.Menu.SlugcatSelectMenu.SlugcatPage.orig_Scroll orig, SlugcatSelectMenu.SlugcatPage self, float timeStacker)
        {
            if (self is SlugcatIllustrationPage page)
                return page.Scroll(timeStacker);
            return orig(self, timeStacker);
        }

        private static bool SlugcatSelectMenu_SlugcatUnlocked(On.Menu.SlugcatSelectMenu.orig_SlugcatUnlocked orig, SlugcatSelectMenu self, SlugcatStats.Name i)
        {
            if (self.saveGameData.Count == 0)
                return true;
            return orig(self, i);
        }


        #endregion

        #region Process

        private static void ProcessManager_PreSwitchMainProcess(On.ProcessManager.orig_PreSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if (self.rainWorld.BuffMode() && ID == ProcessManager.ProcessID.MainMenu)
            {
                int lastSlot = self.rainWorld.options.saveSlot;
                self.rainWorld.options.saveSlot -= 100;

                BuffPlugin.Log($"Change slot from {lastSlot} to {self.rainWorld.options.saveSlot}");
                self.rainWorld.progression.Destroy(lastSlot);
                self.rainWorld.progression = new PlayerProgression(self.rainWorld, true, false);
            }
            orig(self, ID);
        }

        private static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {   
            if (ID == BuffEnums.ProcessID.BuffGameMenu)
            {
                self.currentMainLoop = new BuffGameMenu(self, ID);
            }
            else if (ID == BuffEnums.ProcessID.Cardpedia)
            {
                self.currentMainLoop = new CardpediaMenu(self);
            }
            else if (ID == BuffEnums.ProcessID.BuffGameWinScreen)
            {
                self.currentMainLoop = new BuffGameWinScreen(self);
            }
            else if (ID == BuffEnums.ProcessID.CreditID)
            {
                self.currentMainLoop = new BuffCreditMenu(self);
            }
            else if ((ID == BuffEnums.ProcessID.StackMenu || ID == BuffEnums.ProcessID.UnstackMenu) &&
                     self.oldProcess is SleepAndDeathScreen sleep)
            {
                self.currentMainLoop = new StackAndUnstackMenu(self, sleep.saveState.saveStateNumber, ID);
            }
            if (BuffPoolManager.Instance != null &&
                self.oldProcess is RainWorldGame game)
            {
                BuffPoolManager.Instance.Destroy();
                if (BuffDataManager.Instance.GetGameSetting(game.StoryCharacter).gachaTemplate.CurrentPacket.NeedMenu &&
                    (ID == ProcessManager.ProcessID.SleepScreen || ID == ProcessManager.ProcessID.Dream))
                {
                    self.currentMainLoop = new GachaMenu.GachaMenu(ID, game, self);
                    ID = BuffEnums.ProcessID.GachaMenuID;
                }
            }

            if (ID == ProcessManager.ProcessID.MainMenu)
            {
                if (self.oldProcess is KarmaLadderScreen screen)
                {
                    foreach (var buff in BuffCore.GetAllBuffIds(screen.saveState.saveStateNumber))
                        BuffHookWarpper.DisableBuff(buff, HookLifeTimeLevel.UntilQuit);
                }
                else if (self.oldProcess is RainWorldGame game3)
                {
                    foreach (var buff in BuffCore.GetAllBuffIds(game3.StoryCharacter))
                        BuffHookWarpper.DisableBuff(buff, HookLifeTimeLevel.UntilQuit);
                }
                else if (self.oldProcess is StackAndUnstackMenu menu)
                {
                    foreach (var buff in BuffCore.GetAllBuffIds(menu.name))
                        BuffHookWarpper.DisableBuff(buff, HookLifeTimeLevel.UntilQuit);
                }
                else if (self.oldProcess is BuffGameWinScreen win)
                {
                    foreach (var buff in BuffCore.GetAllBuffIds(win.name))
                        BuffHookWarpper.DisableBuff(buff, HookLifeTimeLevel.UntilQuit);
                }
                BuffDataManager.Instance.CleanMalnourishedData();
            }


            orig(self, ID);
            if (self.currentMainLoop is RainWorldGame game2)
            {
                if (game2.rainWorld.BuffMode())
                {
                    if (BuffPoolManager.Instance == null)
                        BuffPoolManager.LoadGameBuff(game2);
                    ClampKarmaForBuffMode(ref game2.GetStorySession.saveState.deathPersistentSaveData.karma,
                        ref game2.GetStorySession.saveState.deathPersistentSaveData.karmaCap);
                }
                else
                {
                    BuffHookWarpper.CheckAndDisableAllHook();
                }
            }
        }

        #endregion

    }
}

﻿using RandomBuff.Core.Buff;
using RandomBuff.Core.Game;
using RandomBuff.Core.SaveData;
using RandomBuffUtils;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RandomBuff.Core.Option;
using UnityEngine;

namespace RandomBuff.Render.UI
{
    internal abstract class CardInteractionManager: IInputAgencyFocusable
    {
        //常量
        const int Double_ClickThreashold = 10;//单击双击阈值（逻辑帧） 

        public virtual BuffCardSlot BaseSlot { get; protected set; }
        //卡牌信息
        public virtual BuffCard CurrentFocusCard { get; protected set; }
        protected List<BuffCard> managedCards = new List<BuffCard>();

        //状态变量
        bool slateForDeletion;
        CardInteractionManager _overrideManager;
        public CardInteractionManager SubManager//控制覆盖，用于管理多个交互系统。该变量表示对方为低一级的交互系统
        {
            get => _overrideManager;
            set
            {             
                if (value != null)
                    value.overrideDisabled = false;
                _overrideManager = value;
            }
        }
        public bool overrideDisabled;//是否被高级交互系统禁用

        public Vector2 MousePos => InputAgency.Current.GetMousePosition();

        protected bool enableMouseInput = true;

        public CardInteractionManager(BuffCardSlot slot)
        {
            BaseSlot = slot;
        }

        public virtual void Update()
        {
            InputAgency.Current.GetMainFunctionButton(out _, out bool singleClick);
            InputAgency.Current.GetSecondaryFunctionButton(out _, out bool mouseRightSingle);

            if (enableMouseInput && !overrideDisabled)
            {
                if(singleClick) 
                    OnMouseSingleClick();
                if (mouseRightSingle)
                    OnMouseRightClick();
            }
            
            for(int i = managedCards.Count - 1; i >= 0; i--)
            {
                managedCards[i].Update();
            }

            UpdateFocusCard();

            if(SubManager != null && SubManager.slateForDeletion)
            {
                SubManager = null;
            }
        }

        public virtual void GrafUpdate(float timeStacker)
        {
            for (int i = managedCards.Count - 1; i >= 0; i--)
            {
                managedCards[i].GrafUpdate(timeStacker);
            }
        }

        protected virtual void OnMouseSingleClick()
        {

        }

        [Obsolete("目前放弃使用双击以缓解延迟问题，双击功能现由右键替代")]
        protected virtual void OnMouseDoubleClick()
        {
        }

        protected virtual void OnMouseRightClick()
        {
        }

        protected virtual void UpdateFocusCard()
        {

        }

        public virtual void ManageCard(BuffCard card)
        {
            if(card.interactionManager != null)
                card.interactionManager.DismanageCard(card);

            managedCards.Add(card);
            card.interactionManager = this;

            if(BaseSlot != null && !BaseSlot.BuffCards.Contains(card))
                BaseSlot.BuffCards.Add(card);
        }

        public virtual void DismanageCard(BuffCard card)
        {
            if (BaseSlot != null && BaseSlot.BuffCards.Contains(card))
                BaseSlot.BuffCards.Remove(card);

            managedCards.Remove(card);
            card.interactionManager = null;
        }

        public virtual void Destroy()
        {
            slateForDeletion = true;
            SubManager = null;

            foreach(var card in managedCards)
            {
                card.Destroy();
            }
            managedCards.Clear();
        }

        public virtual Vector2? CurrentFocusedObjectPos()
        {
            if (CurrentFocusCard != null)
                return CurrentFocusCard.Position;
            return null;
        }

        public virtual Vector2 GetNextFocusableOjectPos(Vector2 inputDirection)
        {
            inputDirection = inputDirection.normalized;
            Vector2 currentPos = InputAgency.Current.GetMousePosition();

            if (inputDirection == Vector2.zero)
                return currentPos;

            Vector2 nextSelectPosition = Vector2.zero;
            float minAngleDistanceFactor = float.MaxValue;
            //float minDistance = float.MaxValue;
            //float minAngleDelta = float.MaxValue;

            foreach (var buffcardPos in GetAllFocusableObjectPos())
            {
                if (Vector2.Distance(buffcardPos, currentPos) < 10f)
                    continue;

                Vector2 delta = buffcardPos - currentPos;
                float angleFactor = Vector2.Dot(delta.normalized, inputDirection);

                if (angleFactor <= 0)//小于等于0表明位于另一个方向上了
                    continue;

                angleFactor = Mathf.Lerp(1f, 0.2f, angleFactor);//防止等于0乘算导致距离影响因子无效
                float angleDistanceFactor = angleFactor * delta.magnitude;

                if (angleDistanceFactor < minAngleDistanceFactor)
                {
                    minAngleDistanceFactor = angleDistanceFactor;
                    nextSelectPosition = buffcardPos;
                }
            }

            if (nextSelectPosition != Vector2.zero)
                return nextSelectPosition;

            return currentPos;
        }

        public virtual Vector2 GetDefaultFocusableObjectPos()
        {
            if (managedCards.Count == 0)
                return Vector2.zero;
            return managedCards.First().Position;
        }

        public virtual IEnumerable<Vector2> GetAllFocusableObjectPos()
        {
            foreach(var buffcard in managedCards)
                yield return buffcard.Position;
        }
    }

    internal class TestBasicInteractionManager : CardInteractionManager
    {
        public TestBasicInteractionManager(BuffCardSlot slot) : base(slot)
        {
        }

        protected override void UpdateFocusCard()
        {
            foreach(var card in managedCards)
            {
                if(card.LocalMousePos.x > 0 && card.LocalMousePos.x < 1f && card.LocalMousePos.y > 0f && card.LocalMousePos.y < 1f)
                {
                    CurrentFocusCard = card;
                    CurrentFocusCard.SetAnimatorState(BuffCard.AnimatorState.Test_MousePreview);
                    return;
                }
            }

            CurrentFocusCard?.SetAnimatorState(BuffCard.AnimatorState.Test_None);
            CurrentFocusCard = null;
        }

        protected override void OnMouseSingleClick()
        {
            CurrentFocusCard?.OnMouseSingleClick();
        }

        protected override void OnMouseRightClick()
        {
            CurrentFocusCard?.OnMouseRightClick();
        }

        public void DestroyManagedCard()
        {
            if (managedCards.Count > 0)
            {
                foreach (var card in managedCards)
                {
                    card.Destroy();
                }
                managedCards.Clear();
            }
        }
    }

    internal class DoNotingInteractionManager<T> : CardInteractionManager where T : BuffCardSlot
    {
        public T Slot { get => BaseSlot as T; protected set => BaseSlot = value; }

        public DoNotingInteractionManager(T slot) : base(slot)
        {
        }

        public override void Update()
        {
            for (int i = managedCards.Count - 1; i >= 0; i--)
            {
                managedCards[i].Update();
            }
        }
    }

    internal class ClickSignalInteractionManager<T> : CardInteractionManager where T : BuffCardSlot
    {
        public T Slot { get => BaseSlot as T; protected set => BaseSlot = value; }

        public event Action<BuffCard> OnBuffCardSingleClick;
        public event Action<BuffCard> OnBuffCardRightClick;

        public ClickSignalInteractionManager(T slot) : base(slot)
        {
        }

        protected override void UpdateFocusCard()
        {
            if (overrideDisabled)
            {
                if (CurrentFocusCard != null)
                    CurrentFocusCard = null;
                return;
            }

            foreach (var card in managedCards)
            {
                if (card.LocalMousePos.x > 0 &&
                    card.LocalMousePos.x < 1f &&
                    card.LocalMousePos.y > 0f &&
                    card.LocalMousePos.y < 1f)
                {
                    CurrentFocusCard = card;
                    return;
                }
            }

            if (CurrentFocusCard != null)
                CurrentFocusCard = null;
        }

        protected override void OnMouseSingleClick()
        {
            if(CurrentFocusCard != null)
                OnBuffCardSingleClick?.Invoke(CurrentFocusCard);
        }

        protected override void OnMouseRightClick()
        {
            if (CurrentFocusCard != null)
                OnBuffCardRightClick?.Invoke(CurrentFocusCard);
        }
    }

    internal class InGameSlotInteractionManager : CardInteractionManager
    {
        //静态信息
        public static int maxCardBiasCount = 5;

        bool canTriggerBuff;
        public BasicInGameBuffCardSlot Slot { get => BaseSlot as BasicInGameBuffCardSlot; }
        public KeyBinderProcessor keyBinderProcessor;

        public override BuffCard CurrentFocusCard 
        { 
            get => base.CurrentFocusCard; 
            protected set
            {
                if(value != CurrentFocusCard && value != null)
                {
                    if(value.StaticData.BuffType == Core.Buff.BuffType.Positive)
                    {
                        int bias = PositiveBuffCards.IndexOf(value) - PositiveShowIndex;
                        int absBias = Mathf.Abs(bias);
                        if(absBias > maxCardBiasCount)
                        {
                            PositiveShowIndex += (absBias - maxCardBiasCount) * (bias > 0 ? 1 : -1);
                        }
                    }
                    else
                    {
                        int bias = NegativeBuffCards.IndexOf(value) - NegativeShowIndex;
                        int absBias = Mathf.Abs(bias);
                        if (absBias > maxCardBiasCount)
                        {
                            NegativeShowIndex += (absBias - maxCardBiasCount) * (bias > 0 ? 1 : -1);
                        }
                    }
                }
                base.CurrentFocusCard = value;
            }
        }

        State currentState;

        public bool NeedCursor => currentState != State.Hide;

        public int PositiveShowIndex { get; protected set; }
        public int NegativeShowIndex { get; protected set; }

        BuffCard exclusiveShowCard;
        public List<BuffCard> PositiveBuffCards { get; } = new List<BuffCard>();
        public List<BuffCard> NegativeBuffCards { get; } = new List<BuffCard>();

        //Helper.InputButtonTracker toggleShowButtonTracker = new Helper.InputButtonTracker(() => Input.GetKey(ToggleShowButton), false);
        //Helper.InputButtonTracker mouseButtonRightTracker = new Helper.InputButtonTracker(() => Input.GetMouseButton(1), false);

        public InGameSlotInteractionManager(BasicInGameBuffCardSlot slot, bool canTriggerBuff = false) : base(slot)
        {
            this.canTriggerBuff = canTriggerBuff;
            if(canTriggerBuff)
                keyBinderProcessor = new KeyBinderProcessor(this);
        }

        public override void Update()
        {
            base.Update();
            InputAgency.Current.GetToggleHUDButton(out _, out bool showButtonSingle);
            //mouseButtonRightTracker.Update(out bool mouseRightSingle, out bool _);

            if (showButtonSingle && !overrideDisabled)
                OnToggleShowButtonSingleClick();

            keyBinderProcessor?.Update();

            if(currentState == State.ExclusiveShow)
            {
                exclusiveShowCard._cardRenderer.cardTextBackController.CommitScroll(InputAgency.Current.GetScroll() * (InputAgency.CurrentAgencyType == InputAgency.AgencyType.Default ? 2f : 1f));
            }
        }

        protected override void UpdateFocusCard()
        {
            if(overrideDisabled)
            {
                if (CurrentFocusCard != null)
                    CurrentFocusCard = null;
                return;
            }

            if(currentState == State.Show)
            {
                foreach(var card in managedCards)
                {
                    if (card.LocalMousePos.x > 0 && 
                        card.LocalMousePos.x < 1f && 
                        card.LocalMousePos.y > 0f &&
                        card.LocalMousePos.y < 1f)
                    {
                        Slot.HelpInfoProvider.UpdateHelpInfo(BasicInGameBuffCardSlot.InGame_OnMouseFocus, CurrentFocusCard != card, card.ID);
                        CurrentFocusCard = card;
                        return;
                    }
                }

                if (CurrentFocusCard != null)
                {
                    Slot.HelpInfoProvider.UpdateHelpInfo(BasicInGameBuffCardSlot.InGame_NoCardFocus);
                    CurrentFocusCard = null;
                }

            }
            else if(currentState == State.ExclusiveShow)
            {
                if (exclusiveShowCard != null && 
                    exclusiveShowCard.LocalMousePos.x > 0 && 
                    exclusiveShowCard.LocalMousePos.x < 1f && 
                    exclusiveShowCard.LocalMousePos.y > 0f && 
                    exclusiveShowCard.LocalMousePos.y < 1f)
                {
                    CurrentFocusCard = exclusiveShowCard;
                    Slot.HelpInfoProvider.UpdateHelpInfo(BasicInGameBuffCardSlot.InGame_OnCardExclusiveShow, false, CurrentFocusCard.ID);
                    return;
                }

                if (CurrentFocusCard != null) 
                    CurrentFocusCard = null;
            }
            else if(currentState == State.Hide)
            {
                Slot.HelpInfoProvider.UpdateHelpInfo(HelpInfoProvider.HelpInfoID.None);
                if (CurrentFocusCard != null)
                    CurrentFocusCard = null;
            }
        }

        public override void ManageCard(BuffCard card)
        {
            base.ManageCard(card);

            if(card.StaticData.BuffType == Core.Buff.BuffType.Positive)
                PositiveBuffCards.Add(card);
            else
                NegativeBuffCards.Add(card);

            if (currentState == State.Hide)
                card.SetAnimatorState(BuffCard.AnimatorState.InGameSlot_Hide);
            else if (currentState == State.Show)
                card.SetAnimatorState(BuffCard.AnimatorState.InGameSlot_Show);

            keyBinderProcessor?.AppendCard(card);
        }

        public override void DismanageCard(BuffCard card)
        {
            base.DismanageCard(card);

            if (card.StaticData.BuffType == Core.Buff.BuffType.Positive)
                PositiveBuffCards.Remove(card);
            else
                NegativeBuffCards.Remove(card);

            keyBinderProcessor?.RemoveCard(card);
        }

        public int IndexInManagedCards(BuffCard card)
        {
            return managedCards.IndexOf(card);
        }

        public int IndexInGroupedCards(BuffCard card)
        {
            if(card.StaticData.BuffType == Core.Buff.BuffType.Positive)
                return PositiveBuffCards.IndexOf(card);
            return NegativeBuffCards.IndexOf(card);
        }

        public int IndexBiasInGroupedCards(BuffCard card)
        {
            if (card.StaticData.BuffType == Core.Buff.BuffType.Positive)
                return PositiveBuffCards.IndexOf(card) - PositiveShowIndex;
            return NegativeBuffCards.IndexOf(card) - NegativeShowIndex;
        }

        protected void OnToggleShowButtonSingleClick()
        {
            if (currentState == State.Hide)
                SetState(State.Show);
            else if (currentState == State.Show || currentState == State.ExclusiveShow)
                SetState(State.Hide);
        }

        protected override void OnMouseRightClick()
        {
            if (overrideDisabled || !canTriggerBuff)
                return;

            if (currentState == State.Show || currentState == State.ExclusiveShow)
            {
                TriggerCard(CurrentFocusCard);
            }

            //if (currentState == State.ExclusiveShow)
            //{
            //    Slot.RecoverCardSort(exclusiveShowCard);
            //    exclusiveShowCard = null;
            //    SetState(State.Show);
            //}
        }

        protected override void OnMouseSingleClick()
        {
            if(currentState == State.Show)
            {
                if (CurrentFocusCard != null)
                {
                    Slot.BringToTop(CurrentFocusCard);
                    exclusiveShowCard = CurrentFocusCard;
                    SetState(State.ExclusiveShow);
                }

                if(Slot.completeSlot != null)
                {
                    Slot.completeSlot.OpenPocketButton?.OnMouseLeftClick();
                }
            }
            else if(currentState == State.ExclusiveShow)
            {
                if(CurrentFocusCard == null)
                {
                    Slot.RecoverCardSort(exclusiveShowCard);
                    exclusiveShowCard = null;
                    SetState(State.Show);
                }
                else
                    exclusiveShowCard.OnMouseSingleClick();
            }
        }

        public void TriggerCard(BuffID cardID)
        {
            foreach(var card in managedCards)
            {
                if(card.ID == cardID)
                {
                    TriggerCard(card);
                    return;
                }
            }
        }

        void TriggerCard(BuffCard card)
        {
            if (card?.StaticData.Triggerable ?? false)
            {
                card.onMouseRightClick?.Invoke();
                if (BuffPoolManager.Instance.TriggerBuff(card.ID))
                {
                    if (exclusiveShowCard == card)
                        exclusiveShowCard = null;
                    if (CurrentFocusCard == card)
                        CurrentFocusCard = null;
                    SetState(State.Show);
                }
            }
        }

        public void SetState(State newState)
        {
            if (currentState == newState)
                return;

            currentState = newState;
            if(newState == State.Hide)
            {
                Slot.BackDark = false;
                Slot.FrontDark = false;
                foreach (var card in managedCards)
                    card.SetAnimatorState(BuffCard.AnimatorState.InGameSlot_Hide);

                if (SubManager != null) SubManager.overrideDisabled = false;
                if (Slot.completeSlot != null)
                {
                    Slot.completeSlot.ConditionHUD.ChangeMode(BuffCondition.BuffConditionHUD.Mode.Refresh);
                    Slot.completeSlot.SetPocketButtonShow(false);
                }
                Slot.completeSlot?.Title?.ChangeTitle("", true);

                Slot.completeSlot?.SetGamePaused(false);
                InputAgency.Current.RecoverLastIfIsFocus(this, false);
            }
            else if(newState == State.Show)
            {
                Slot.BackDark = true;
                Slot.FrontDark = false;
                foreach (var card in managedCards)
                    card.SetAnimatorState(BuffCard.AnimatorState.InGameSlot_Show);
                if (SubManager != null) SubManager.overrideDisabled = true;

                if (Slot.completeSlot != null)
                {
                    Slot.completeSlot.ConditionHUD.ChangeMode(BuffCondition.BuffConditionHUD.Mode.Alway);
                    Slot.completeSlot.SetPocketButtonShow(true);
                    Slot.completeSlot.Title?.ChangeTitle(BuffResourceString.Get("InGameSlot_SlotTitle"), true);
                    Slot.completeSlot.SetGamePaused(true);
                    Slot.completeSlot.HideTips();
                }
                 
                InputAgency.Current.TakeFocus(this);

                //用于在动画完成时更新鼠标位置
                AnimMachine.GetDelayCmpnt(30, autoDestroy: true).BindActions(OnAnimFinished: (d) =>
                {
                    InputAgency.Current.ResetToDefaultPos();
                });
            }
            else if(newState == State.ExclusiveShow)
            {
                Slot.FrontDark = true;
                exclusiveShowCard.SetAnimatorState(BuffCard.AnimatorState.InGameSlot_Exclusive_Show);

                Slot.completeSlot?.Title?.ChangeTitle(BuffResourceString.Get("InGameSlot_CardDetail"), true);
                if (SubManager != null) SubManager.overrideDisabled = true;

                Slot.completeSlot?.SetGamePaused(true);
            }
        }

        public override Vector2 GetNextFocusableOjectPos(Vector2 inputDirection)
        {
            if (currentState == State.ExclusiveShow)
                return exclusiveShowCard.Position;
            else if (currentState == State.Hide)
                return Vector2.zero;
            return base.GetNextFocusableOjectPos(inputDirection);
        }

        public override IEnumerable<Vector2> GetAllFocusableObjectPos()
        {
            foreach(var item in base.GetAllFocusableObjectPos())
                yield return item;

            if (Slot.completeSlot != null && Slot.completeSlot.OpenPocketButton != null)
                yield return Slot.completeSlot.OpenPocketButton.MiddleOfButton();
        }

        public class KeyBinderProcessor
        {
            public List<BuffID> triggerableBuffIDs = new List<BuffID>();

            InGameSlotInteractionManager manager;

            string lastKey;
            bool keyAlreadyGetted;
            public bool listenerEnable;

            public bool InBindMode => listenerEnable && !keyAlreadyGetted;

            public KeyBinderProcessor(InGameSlotInteractionManager manager)
            {
                this.manager = manager;
            }

            public void AppendCard(BuffCard card)
            {
                if(card.StaticData.Triggerable)
                    triggerableBuffIDs.Add(card.ID);
            }

            public void RemoveCard(BuffCard card)
            {
                if (card.StaticData.Triggerable)
                    triggerableBuffIDs.Remove(card.ID);
            }

            public void Update()
            {
                if (manager.currentState == State.ExclusiveShow)
                {
                    if (BuffInput.GetKey(BuffOptionInterface.Instance.KeyBindKey.Value))
                    {
                        if (!listenerEnable)
                            EnableListen();

                        if (GetKey(out var key))
                        {
                            BuffPlayerData.Instance.SetKeyBind(manager.exclusiveShowCard.ID, key);
                            manager.exclusiveShowCard.UpdateGraphText(true);
                        }
                    }
                    else
                    {
                        if (listenerEnable)
                            DisableListen();
                    }
                }
                else
                {
                    for(int i = triggerableBuffIDs.Count - 1; i >= 0; i--)
                    {
                        var id = triggerableBuffIDs[i];
                        if (BuffInput.GetKeyDown(BuffPlayerData.Instance.GetKeyBind(id)))
                        {
                            BuffPlugin.Log($"Trigger card {id} by shorcut key {BuffPlayerData.Instance.GetKeyBind(id)}");
                            manager.TriggerCard(id);
                        }
                    }
                }
            }

            private void BuffInput_OnAnyKeyDown(string keyDown)
            {
                if (lastKey == null && !ExclusiveThisKey(keyDown))
                {
                    lastKey = keyDown;
                }
            }

            public void EnableListen()
            {
                BuffPlugin.Log("Keybinder enable listen");
                listenerEnable = true;
                keyAlreadyGetted = false;
                lastKey = null;
                BuffInput.OnAnyKeyDown += BuffInput_OnAnyKeyDown;
            }
            
            public void DisableListen()
            {
                BuffPlugin.Log("Keybinder disable listen");
                listenerEnable = false;
                lastKey = null;
                keyAlreadyGetted = false;
                BuffInput.OnAnyKeyDown -= BuffInput_OnAnyKeyDown;
            }

            public bool GetKey(out string key)
            {
                if (listenerEnable && lastKey != null && !keyAlreadyGetted)
                {
                    BuffPlugin.Log($"Keybinder get key of {lastKey}");
                    keyAlreadyGetted = true;
                    key = lastKey;
                    return true;
                }
                key = null;
                return false;
            }

            static bool ExclusiveThisKey(string key)
            {
                if (key.StartsWith("Mouse"))
                    return true;
                if (key == BuffOptionInterface.Instance.KeyBindKey.Value)
                    return true;
                if(key.Contains("Command"))
                    return true;
                return false;
            }
        }

        public enum State
        {
            Hide,
            Show,
            ExclusiveShow
        }
    }

    internal class CardPickerInteractionManager : CardInteractionManager
    {
        public static int maxCardBiasCount = 2;

        bool finishSelection;

        public CardPickerSlot Slot { get => BaseSlot as CardPickerSlot; }

        public List<BuffCard> MajorCard { get; private set; } = new List<BuffCard>();
        public List<BuffCard> AdditionalCard { get; private set; } = new List<BuffCard>();

        public Dictionary<BuffCard, BuffCard> Additional2MajorMapper { get; private set; } = new Dictionary<BuffCard, BuffCard>();
        public Dictionary<BuffCard, BuffCard> Major2AdditionalMapper { get; private set; } = new Dictionary<BuffCard, BuffCard>();

        public int CardShowIndex { get; private set; }

        public override BuffCard CurrentFocusCard
        {
            get => base.CurrentFocusCard;
            protected set
            {
                if (value != CurrentFocusCard && value != null)
                {
                    int bias = GetCardShowIndex(value, out int _);
                    int absBias = Mathf.Abs(bias);
                    if (absBias > maxCardBiasCount)
                    {
                        CardShowIndex += (absBias - maxCardBiasCount) * (bias > 0 ? 1 : -1);
                        BuffPlugin.Log($"focus on {value.ID}, make show index to {CardShowIndex}, bias {bias}, absBias{absBias}");
                    }
                }
                base.CurrentFocusCard = value;
            }
        }

        public CardPickerInteractionManager(CardPickerSlot slot) : base(slot)
        {
        }

        public void FinishManage()
        {
            foreach(var card in managedCards)
            {
                card.SetAnimatorState(BuffCard.AnimatorState.CardPicker_Show);
            }
            CardShowIndex = Mathf.RoundToInt(MajorCard.Count / 2f) - 1;
        }

        public void FinishSelection()
        {
            finishSelection = true;
            foreach(var card in managedCards)
            {
                card.SetAnimatorState(BuffCard.AnimatorState.CardPicker_Disappear);
            }
        }

        public void ManageMajorCard(BuffCard card)
        {
            ManageCard(card);
            MajorCard.Add(card);

            BuffPlugin.Log($"Manage major : {card.ID}");
        }

        public void ManageAddtionalCard(BuffCard card, BuffCard linkedMajorCard)
        {
            ManageCard(card);
            AdditionalCard.Add(linkedMajorCard);

            Additional2MajorMapper.Add(card, linkedMajorCard);
            Major2AdditionalMapper.Add(linkedMajorCard, card);

            BuffPlugin.Log($"Manage additional : {card.ID} linked with : {linkedMajorCard.ID}");
        }

        public override void ManageCard(BuffCard card)
        {
            base.ManageCard(card);
            BuffPlugin.Log($"Card picker manage card {card.ID}, {Slot.BuffCards.Count} in total");
        }

        public override void DismanageCard(BuffCard card)
        {
            if (MajorCard.Contains(card))
                MajorCard.Remove(card);

            if (AdditionalCard.Contains(card))
                Additional2MajorMapper.Remove(card);
            base.DismanageCard(card);
            //Helper.TraceStack();
            BuffPlugin.Log($"Card picker dismanage card {card.ID}, {Slot.BuffCards.Count} remains");
        }

        /// <summary>
        /// 获取卡牌的聚焦坐标
        /// </summary>
        /// <param name="buffCard"></param>
        /// <param name="majorOrAdditional">为0时为普通抽卡，为1时为加强卡主卡，为-1时为加强卡附卡</param>
        /// <returns></returns>
        public int GetCardShowIndex(BuffCard buffCard, out int majorOrAdditional)
        {
            return GetCardIndex(buffCard, out majorOrAdditional) - CardShowIndex;
        }

        public int GetCardIndex(BuffCard buffCard, out int majorOrAdditional)
        {
            if (Additional2MajorMapper.TryGetValue(buffCard, out var major))
            {
                majorOrAdditional = -1;
                return GetCardIndex(major, out var _);
            }
            if (Major2AdditionalMapper.TryGetValue(buffCard, out var _))
            {
                majorOrAdditional = 1;
            }
            else
                majorOrAdditional = 0;
            return MajorCard.IndexOf(buffCard);
        }

        protected override void UpdateFocusCard()
        {
            if(overrideDisabled || finishSelection)
            {
                Slot.HelpInfoProvider.UpdateHelpInfo(HelpInfoProvider.HelpInfoID.None);
                if (CurrentFocusCard != null)
                    CurrentFocusCard = null;
                return;
            }

            foreach (var card in managedCards)
            {
                if (card.LocalMousePos.x > 0 &&
                    card.LocalMousePos.x < 1f &&
                    card.LocalMousePos.y > 0f &&
                    card.LocalMousePos.y < 1f)
                {
                    Slot.HelpInfoProvider.UpdateHelpInfo(CardPickerSlot.CardPicker_OnMouseFocus, CurrentFocusCard != card);
                    CurrentFocusCard = card;
                    return;
                }
            }

            if (CurrentFocusCard != null)
            {
                Slot.HelpInfoProvider.UpdateHelpInfo(CardPickerSlot.CardPicker_NoCardFocus, false);
                CurrentFocusCard = null;
            }
        }

        protected override void OnMouseSingleClick()
        {
            if(CurrentFocusCard != null)
            {
                CurrentFocusCard.OnMouseSingleClick();
                if (Major2AdditionalMapper.TryGetValue(CurrentFocusCard, out var additional))
                    additional.OnMouseSingleClick();

                if (Additional2MajorMapper.TryGetValue(CurrentFocusCard, out var major))
                    major.OnMouseSingleClick();
            }
        }

        protected override void OnMouseRightClick()
        {
            if (CurrentFocusCard != null)
            {
                CurrentFocusCard.OnMouseRightClick();
                Slot.CardPicked(CurrentFocusCard);

                //if (Major2AdditionalMapper.TryGetValue(CurrentFocusCard, out var additional))
                //{
                //    BuffPlugin.Log($"{CurrentFocusCard.ID} => {additional.ID}");
                //    additional.OnMouseRightClick();
                //    Slot.CardPicked(additional);
                //}

                //if (Additional2MajorMapper.TryGetValue(CurrentFocusCard, out var major))
                //{
                //    major.OnMouseRightClick();
                //    Slot.CardPicked(additional);
                //}
            }
        }
    }
}

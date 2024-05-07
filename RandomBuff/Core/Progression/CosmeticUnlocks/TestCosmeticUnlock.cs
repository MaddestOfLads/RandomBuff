﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomBuff.Core.Progression.CosmeticUnlocks
{
    internal class TestCosmeticUnlock : CosmeticUnlock
    {
        public override CosmeticUnlockID UnlockID => CosmeticUnlockID.Test;
        public override void StartGame(RainWorldGame game)
        {
            base.StartGame(game);
            BuffPlugin.LogDebug("Test Start");
        }
    }
}
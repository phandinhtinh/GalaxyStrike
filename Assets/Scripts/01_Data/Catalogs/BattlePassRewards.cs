using System;
using System.Collections.Generic;

namespace GalaxyStrike.Data
{
    public static class BattlePassRewards
    {
        public readonly struct Tier
        {
            public int RequiredExperience { get; }
            public int Coins { get; }

            public Tier(int requiredExperience, int coins)
            {
                RequiredExperience = requiredExperience;
                Coins = coins;
            }
        }

        // SaveData stores claims by index. Keep existing tiers in this order.
        public static IReadOnlyList<Tier> Tiers { get; } = Array.AsReadOnly(new[]
        {
            new Tier(100, 100),
            new Tier(250, 200),
            new Tier(450, 350)
        });
    }
}

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
            public int Gems { get; }
            public int Energy { get; }

            public Tier(int requiredExperience, int coins, int gems = 0, int energy = 0)
            {
                RequiredExperience = requiredExperience;
                Coins = coins;
                Gems = gems;
                Energy = energy;
            }
        }

        // SaveData stores claims by index in a 32-bit mask (max 31 tiers).
        // Keep existing tiers in this order and only append new ones.
        public static IReadOnlyList<Tier> Tiers { get; } = Array.AsReadOnly(new[]
        {
            new Tier(100, 100),
            new Tier(250, 200),
            new Tier(450, 350),
            new Tier(800, 0, gems: 10),
            new Tier(1300, 500),
            new Tier(2000, 0, energy: 10),
            new Tier(3000, 800),
            new Tier(4500, 0, gems: 20),
            new Tier(6500, 1200),
            new Tier(9000, 0, energy: 20),
            new Tier(12000, 2000),
            new Tier(16000, 0, gems: 40),
            new Tier(20000, 3000, gems: 50)
        });
    }
}

using NUnit.Framework;

public class VictoryStarsTests
{
    [TestCase(3, 3, false, 3)]
    [TestCase(2, 3, false, 2)]
    [TestCase(1, 3, false, 1)]
    [TestCase(8, 8, false, 3)]
    [TestCase(7, 8, false, 3)]
    [TestCase(4, 8, false, 2)]
    [TestCase(3, 8, false, 1)]
    [TestCase(3, 3, true, 1)]
    [TestCase(0, 0, false, 1)]
    public void Rate_UsesRemainingHullAndRevive(int health, int maxHealth, bool revived, int expected)
    {
        Assert.AreEqual(expected, VictoryStars.Rate(health, maxHealth, revived));
    }
}

namespace Moba.Duel
{
    public static class DuelNames
    {
        public const string Ogre = "Ogre";
        public const string OrbKeeper = "Miravel";

        public static string For(DuelHero hero)
        {
            return hero == DuelHero.Ogre ? Ogre : OrbKeeper;
        }
    }
}

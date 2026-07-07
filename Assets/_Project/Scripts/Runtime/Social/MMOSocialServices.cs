namespace RPGClone.Social
{
    public static class MMOSocialServices
    {
        private static MMORuntimeSocialService runtimeService;

        public static ICharacterNameDirectory CharacterNames => Local;
        public static IFriendListService Friends => Local;
        public static ICharacterPresenceService Presence => Local;
        public static ISessionPresenceService Sessions => Local;
        public static IInviteService Invites => Local;

        private static MMORuntimeSocialService Local => runtimeService ??= new MMORuntimeSocialService();
    }
}

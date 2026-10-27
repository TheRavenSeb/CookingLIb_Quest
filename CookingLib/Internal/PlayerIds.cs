namespace CookingLib
{
    internal static class PlayerIds
    {
        internal static Player Of(ConsumptionManager consumer)
        {
            if (consumer == null)
                return null;
            PlayerController controller = consumer.Player;
            Player player = controller == null ? null : controller.NetworkPlayer;
            return player == null ? null : player;
        }

        internal static UserInfoAndRole InfoOf(Player player) => player == null ? null : player.ConnectionToRemotePlayer?.UserInfo;
    }
}

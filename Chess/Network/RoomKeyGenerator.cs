namespace SuperpositionChess.Network
{
    public static class RoomKeyGenerator
    {
        private static readonly Random random = new();

        public static string GenerateKey()
        {
            // 6-значный цифровой код
            return random.Next(100000, 999999).ToString();
        }
    }
}
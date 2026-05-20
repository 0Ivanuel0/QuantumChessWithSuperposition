namespace SuperpositionChess.Network
{
    public static class RoomKeyGenerator
    {
        private static readonly Random _random = new();

        public static string GenerateKey()
        {
            // 6-значный цифровой код
            return _random.Next(100000, 999999).ToString();
        }
    }
}
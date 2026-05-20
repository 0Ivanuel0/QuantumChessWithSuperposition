using System.Text.Json;

namespace SuperpositionChess.Network
{
    [Serializable]
    public class NetworkMoveData
    {
        public int FromRow { get; set; }
        public int FromCol { get; set; }
        public int ToRow { get; set; }
        public int ToCol { get; set; }

        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        public static NetworkMoveData? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<NetworkMoveData>(json);
        }
    }
}
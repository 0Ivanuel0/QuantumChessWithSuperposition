using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SuperpositionChess.Network
{
    public class NetworkGameClient
    {
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;

        public event Action<NetworkMoveData>? OnMoveReceived;
        public event Action? OnConnected;
        public event Action<string>? OnError;

        public bool IsConnected => client?.Connected ?? false;

        public async Task<bool> ConnectAsync(string host, int port, string roomKey)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(host, port);

                var stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                await writer.WriteLineAsync(roomKey);

                var response = await reader.ReadLineAsync();
                if (response != "OK")
                {
                    client.Close();
                    OnError?.Invoke("Неверный ключ комнаты!");
                    return false;
                }

                OnConnected?.Invoke();
                _ = ListenForMoves();
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        private async Task ListenForMoves()
        {
            while (client?.Connected ?? false)
            {
                try
                {
                    var line = await reader!.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        break;

                    var moveData = JsonSerializer.Deserialize<NetworkMoveData>(line);
                    if (moveData != null)
                        OnMoveReceived?.Invoke(moveData);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex.Message);
                    break;
                }
            }
        }

        public async Task SendMoveAsync(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (writer == null || client == null || !client.Connected)
                return;

            var moveData = new NetworkMoveData
            {
                FromRow = fromRow,
                FromCol = fromCol,
                ToRow = toRow,
                ToCol = toCol
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(moveData));
        }

        public void Disconnect()
        {
            client?.Close();
            client = null;
        }
    }
}
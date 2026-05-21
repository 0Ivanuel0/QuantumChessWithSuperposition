using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SuperpositionChess.Network
{
    public class NetworkGameServer
    {
        private TcpListener? listener;
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;
        private readonly int port;
        private readonly string roomKey;

        public event Action<NetworkMoveData>? OnMoveReceived;
        public event Action? OnClientConnected;
        public event Action<string>? OnError;

        public string RoomKey => roomKey;
        public bool IsConnected => client?.Connected ?? false;

        public NetworkGameServer(int port, string roomKey)
        {
            this.port = port;
            this.roomKey = roomKey;
        }

        public async Task StartAsync()
        {
            listener?.Stop();
            client?.Close();

            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();

                while (true)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync();
                    var stream = tcpClient.GetStream();
                    reader = new StreamReader(stream, Encoding.UTF8);
                    writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                    var key = await reader.ReadLineAsync();
                    if (key != roomKey)
                    {
                        await writer.WriteLineAsync("WRONG_KEY");
                        tcpClient.Close();
                        continue;
                    }

                    await writer.WriteLineAsync("OK");
                    client = tcpClient;
                    OnClientConnected?.Invoke();
                    _ = ListenForMoves();
                    break;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка сервера: {ex.Message}");
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

        public void Stop()
        {
            client?.Close();
            listener?.Stop();
        }
    }
}
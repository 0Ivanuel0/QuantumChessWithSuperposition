using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SuperpositionChess.Network
{
    public class NetworkGameServer
    {
        private TcpListener _listener;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly int _port;
        private readonly string _roomKey;

        private StreamReader? _reader;
        private StreamWriter? _writer;

        public event Action<NetworkMoveData>? OnMoveReceived;
        public event Action? OnClientConnected;
        public event Action<string>? OnError;

        public string RoomKey => _roomKey;
        public bool IsConnected => _client?.Connected ?? false;

        public NetworkGameServer(int port, string roomKey)
        {
            _port = port;
            _roomKey = roomKey;
        }

        public async Task StartAsync()
        {

            _listener?.Stop();
            _client?.Close();
            _stream?.Close();

            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);

                _listener.Start();

                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();

                    var stream = client.GetStream();

                    _reader = new StreamReader(stream, Encoding.UTF8);

                    _writer = new StreamWriter(stream, new UTF8Encoding(false))
                    {
                        AutoFlush = true
                    };

                    string? key = await _reader.ReadLineAsync();

                    if (key != _roomKey)
                    {
                        await _writer.WriteLineAsync("WRONG_KEY");

                        client.Close();

                        continue;
                    }

                    await _writer.WriteLineAsync("OK");

                    _client = client;
                    _stream = stream;

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
            while (_client?.Connected ?? false)
            {
                try
                {
                    string? line = await _reader!.ReadLineAsync();

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
            if (_writer == null || _client == null || !_client.Connected)
                return;

            var moveData = new NetworkMoveData
            {
                FromRow = fromRow,
                FromCol = fromCol,
                ToRow = toRow,
                ToCol = toCol
            };

            string json = JsonSerializer.Serialize(moveData);

            await _writer.WriteLineAsync(json);
        }

        public void Stop()
        {
            _stream?.Close();
            _client?.Close();
            _listener?.Stop();
        }
    }
}
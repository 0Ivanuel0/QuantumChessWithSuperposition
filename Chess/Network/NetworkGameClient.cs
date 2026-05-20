using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SuperpositionChess.Network
{
    public class NetworkGameClient
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        private StreamReader? _reader;
        private StreamWriter? _writer;

        public event Action<NetworkMoveData>? OnMoveReceived;
        public event Action? OnConnected;
        public event Action<string>? OnError;

        public bool IsConnected => _client?.Connected ?? false;

        public async Task<bool> ConnectAsync(string host, int port, string roomKey)
        {
            try
            {
                _client = new TcpClient();

                await _client.ConnectAsync(host, port);

                _stream = _client.GetStream();

                _reader = new StreamReader(_stream, Encoding.UTF8);

                _writer = new StreamWriter(_stream, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

                // Отправляем ключ
                await _writer.WriteLineAsync(roomKey);

                // Получаем ответ
                string? response = await _reader.ReadLineAsync();

                if (response != "OK")
                {
                    _client.Close();

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

        public void Disconnect()
        {
            _stream?.Close();
            _client?.Close();
        }
    }
}
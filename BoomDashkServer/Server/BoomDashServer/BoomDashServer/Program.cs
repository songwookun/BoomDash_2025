using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        MatchServer.Start();
    }
}

public enum MessageType
{
    CreateRoom,
    JoinRoom,
    StartGame,
    RoomList,
    Error
}

public class GameMessage
{
    public MessageType Type { get; set; }
    public string Data { get; set; } = string.Empty;
}

public class Room
{
    public string Name { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string Password { get; set; } = string.Empty;
    public int MaxPlayers { get; set; }
    public List<MatchServer.ClientState> Players { get; set; } = new();
}

public static class MatchServer
{
    private static TcpListener? listener;
    private const int port = 7777;
    private static Dictionary<string, Room> rooms = new();
    private static int playerCount = 0;

    public static void Start()
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"[서버] 시작됨 - 포트 {port}");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Task.Run(() => HandleClient(new ClientState(client)));
        }
    }

    static async Task HandleClient(ClientState client)
    {
        playerCount++;
        client.nickname = $"UnityPlayer{playerCount}";

        Console.WriteLine($"[연결] {client.nickname} ({client.id}) 연결됨");

        try
        {
            while (true)
            {
                string? line = await client.reader.ReadLineAsync();
                if (line == null) break;
                Console.WriteLine($"[수신] {client.nickname}: {line}");

                var msg = JsonSerializer.Deserialize<GameMessage>(line);
                if (msg == null) continue;

                switch (msg.Type)
                {
                    case MessageType.CreateRoom:
                        HandleCreateRoom(client, msg.Data);
                        break;

                    case MessageType.JoinRoom:
                        HandleJoinRoom(client, msg.Data);
                        break;

                    case MessageType.RoomList:
                        HandleRoomList(client);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[에러] {ex.Message}");
        }
        finally
        {
            client.client.Close();
        }
    }

    static void HandleCreateRoom(ClientState client, string data)
    {
        var room = JsonSerializer.Deserialize<Room>(data);
        if (room == null || rooms.ContainsKey(room.Name))
        {
            client.Send(MessageType.Error, "방 생성 실패 또는 이미 존재합니다.");
            return;
        }

        room.Players.Add(client);
        rooms.Add(room.Name, room);
        Console.WriteLine($"[방 생성] {room.Name} (공개 여부: {(room.IsPrivate ? "비공개" : "공개")})");

        BroadcastRoomList();
    }

    static void HandleJoinRoom(ClientState client, string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        string roomName = root.GetProperty("roomName").GetString() ?? "";
        string password = root.GetProperty("password").GetString() ?? "";

        if (!rooms.TryGetValue(roomName, out var room))
        {
            client.Send(MessageType.Error, "방이 존재하지 않습니다.");
            return;
        }

        if (room.IsPrivate && room.Password != password)
        {
            client.Send(MessageType.Error, "비밀번호가 일치하지 않습니다.");
            return;
        }

        if (room.Players.Count >= room.MaxPlayers)
        {
            client.Send(MessageType.Error, "방이 가득 찼습니다.");
            return;
        }

        room.Players.Add(client);
        Console.WriteLine($"[입장 성공] {client.nickname} → {room.Name} ({room.Players.Count}/{room.MaxPlayers})");

        if (room.Players.Count == room.MaxPlayers)
        {
            foreach (var p in room.Players)
            {
                p.Send(MessageType.StartGame, room.Name);
            }
        }
    }

    static void HandleRoomList(ClientState client)
    {
        var summaries = new List<object>();

        foreach (var kvp in rooms)
        {
            summaries.Add(new
            {
                name = kvp.Value.Name,
                isPrivate = kvp.Value.IsPrivate,
                current = kvp.Value.Players.Count,
                max = kvp.Value.MaxPlayers
            });
        }

        string json = JsonSerializer.Serialize(summaries);
        client.Send(MessageType.RoomList, json);
    }

    static void BroadcastRoomList()
    {
        foreach (var room in rooms.Values)
        {
            foreach (var player in room.Players)
            {
                HandleRoomList(player);
            }
        }
    }

    public class ClientState
    {
        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        public string id = Guid.NewGuid().ToString();
        public string nickname = "";

        public ClientState(TcpClient c)
        {
            client = c;
            var stream = c.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };
        }

        public void Send(MessageType type, string data)
        {
            var msg = new GameMessage { Type = type, Data = data };
            string json = JsonSerializer.Serialize(msg);
            writer.WriteLine(json);
            Console.WriteLine($"[전송 → {nickname}] {type} : {data}");
        }
    }
}

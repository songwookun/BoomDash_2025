using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
    Error,
    MyOrder,
    Move
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

public class JoinRoomRequest
{
    public string roomName { get; set; }
    public string password { get; set; }
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

                var msg = JsonConvert.DeserializeObject<GameMessage>(line);
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
                    case MessageType.MyOrder:
                        HandleMyOrder(client, msg.Data);
                        break;
                    case MessageType.Move:
                        RelayMovement(client, msg.Data);
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
            CleanupClient(client);
        }
    }

    static void CleanupClient(ClientState client)
    {
        Console.WriteLine($"[정리] {client.nickname} 연결 해제 및 방에서 제거");
        var roomsToRemove = new List<string>();

        foreach (var kvp in rooms)
        {
            var room = kvp.Value;
            if (room.Players.Contains(client))
            {
                room.Players.Remove(client);
                Console.WriteLine($"[방 퇴장] {client.nickname} → {room.Name}");
                if (room.Players.Count == 0)
                    roomsToRemove.Add(room.Name);
            }
        }

        foreach (var roomName in roomsToRemove)
        {
            rooms.Remove(roomName);
            Console.WriteLine($"[방 삭제] {roomName} (빈 방)");
        }

        try { client.reader?.Close(); } catch { }
        try { client.writer?.Close(); } catch { }
        try { client.client?.Close(); } catch { }
    }

    static void HandleCreateRoom(ClientState client, string data)
    {
        var room = JsonConvert.DeserializeObject<Room>(data);
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
        var joinData = JsonConvert.DeserializeObject<JoinRoomRequest>(data);
        string roomName = joinData?.roomName ?? "";
        string password = joinData?.password ?? "";

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
        string json = JsonConvert.SerializeObject(summaries);
        client.Send(MessageType.RoomList, json);
    }

    static void BroadcastRoomList()
    {
        var clientsToRemove = new List<ClientState>();
        foreach (var room in rooms.Values)
        {
            foreach (var player in room.Players)
            {
                try { HandleRoomList(player); }
                catch { clientsToRemove.Add(player); }
            }
        }
        foreach (var client in clientsToRemove)
        {
            CleanupClient(client);
        }
    }

    static void HandleMyOrder(ClientState client, string roomName)
    {
        Console.WriteLine($"[MyOrder 요청] {client.nickname} → {roomName}");
        if (rooms.TryGetValue(roomName, out var room))
        {
            int index = room.Players.IndexOf(client);
            if (index != -1)
            {
                client.Send(MessageType.MyOrder, index.ToString());
                Console.WriteLine($"[순번 전송] {client.nickname} → {index}");
            }
            else
            {
                client.Send(MessageType.Error, "방에서 클라이언트를 찾을 수 없습니다.");
            }
        }
        else
        {
            client.Send(MessageType.Error, "해당 방이 존재하지 않습니다.");
        }
    }

    static void RelayMovement(ClientState sender, string data)
    {
        foreach (var room in rooms.Values)
        {
            if (room.Players.Contains(sender))
            {
                foreach (var player in room.Players)
                {
                    if (player != sender)
                    {
                        player.Send(MessageType.Move, data);
                    }
                }
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
            try
            {
                var msg = new GameMessage { Type = type, Data = data };
                string json = JsonConvert.SerializeObject(msg);
                writer.WriteLine(json);
                Console.WriteLine($"[전송 → {nickname}] {type} : {data}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[전송 실패] {nickname}: {ex.Message}");
            }
        }
    }
}

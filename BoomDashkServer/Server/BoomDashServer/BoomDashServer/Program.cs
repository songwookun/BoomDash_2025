#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
    Move,
    ItemSpawn,
    ItemPickup,
    ItemRemove,
    ApplyBuff,
    ScoreUpdate,
    BagUpdate,
    DepositBag,
    RequestRematch,
    ExitToLobby,
    ReturnToLobby,
    TimerSync,  
    MatchOver   
}

public class GameMessage
{
    public MessageType Type { get; set; }
    public string Data { get; set; } = string.Empty;
}

public enum EffectType
{
    None = 0,
    Score = 1,
    PlayerMoveSpeedUp = 2
}

public class ItemDef
{
    public int itemID;
    public string itemName = "";
    public string itemCategory = "";
    public EffectType effectType = EffectType.None;
    public float value1 = 0f;
    public float value2 = 0f;
    public float duration = 0f;
}

public class Room
{
    public string Name { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string Password { get; set; } = string.Empty;
    public int MaxPlayers { get; set; }
    public List<MatchServer.ClientState> Players { get; set; } = new();

    public CancellationTokenSource? DropCts { get; set; }
    public HashSet<string> ActiveItemIds { get; set; } = new();
    public Dictionary<string, int> ItemMap { get; set; } = new();

    public CancellationTokenSource? MatchCts { get; set; }
    public int SecondsLeft { get; set; } = 0;
    public bool IsMatchRunning =>
        MatchCts != null && !MatchCts.IsCancellationRequested && SecondsLeft > 0;
}

public class JoinRoomRequest
{
    public string roomName { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty; 
}

public class ItemDropEntry
{
    public int itemID;
    public string itemName = "";
    public int dropQuantityMin;
    public int dropQuantityMax;
    public int dropTime;
}

public static class MatchServer
{
    private static TcpListener? listener;
    private const int port = 7777;
    private static readonly Dictionary<string, Room> rooms = new();
    private static int playerCount = 0;

    private static readonly List<ItemDropEntry> dropTable = new();
    private static readonly Dictionary<int, ItemDef> itemDefs = new();

    private const float minX = -8f, maxX = 8f, minY = -4f, maxY = 4f;

    public static void Start()
    {
        LoadItemDefs();
        LoadDropTable();

        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"[서버] 시작됨 - 포트 {port}");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            _ = Task.Run(() => HandleClient(new ClientState(client)));
        }
    }

    private static void LoadItemDefs()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Item.csv");
            if (!File.Exists(path))
            {
                Console.WriteLine($"[경고] Item.csv 없음: {path}");
                return;
            }

            var lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++)
            {
                var raw = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (raw.StartsWith("#")) continue;

                var cols = raw.Split(',');
                if (cols.Length < 7)
                {
                    Console.WriteLine($"[Item.csv] 형식 오류: {raw}");
                    continue;
                }

                try
                {
                    var def = new ItemDef
                    {
                        itemID = int.Parse(cols[0]),
                        itemName = cols[1],
                        itemCategory = cols[2],
                        effectType = Enum.TryParse<EffectType>(cols[3], out var et) ? et : EffectType.None,
                        value1 = float.Parse(cols[4]),
                        value2 = float.Parse(cols[5]),
                        duration = float.Parse(cols[6])
                    };
                    itemDefs[def.itemID] = def;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Item.csv] 잘못된 라인 무시: {raw} ({ex.Message})");
                }
            }
            Console.WriteLine($"[Item.csv] {itemDefs.Count}개 로드");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Item.csv 로드 오류] {ex.Message}");
        }
    }

    private static void LoadDropTable()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "ItemDrop.csv");
            if (!File.Exists(path))
            {
                Console.WriteLine($"[경고] ItemDrop.csv 없음: {path}");
                return;
            }

            var lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++)
            {
                var raw = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (raw.StartsWith("#")) continue;

                var cols = raw.Split(',');
                if (cols.Length < 5) continue;

                try
                {
                    var e = new ItemDropEntry
                    {
                        itemID = int.Parse(cols[0]),
                        itemName = cols[1], 
                        dropQuantityMin = int.Parse(cols[2]),
                        dropQuantityMax = int.Parse(cols[3]),
                        dropTime = int.Parse(cols[4])
                    };
                    dropTable.Add(e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[드랍테이블] 잘못된 라인 무시: {raw} ({ex.Message})");
                }
            }

            Console.WriteLine($"[드랍테이블] {dropTable.Count}개 로드");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[드랍테이블 로드 오류] {ex.Message}");
        }
    }

    private static async Task HandleClient(ClientState client)
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

                    case MessageType.ItemPickup:
                        HandleItemPickup(client, msg.Data);
                        break;

                    case MessageType.DepositBag:
                        HandleDepositBag(client);
                        break;

                    case MessageType.RequestRematch:
                        HandleRequestRematch(client);
                        break;

                    case MessageType.ExitToLobby:
                        HandleExitToLobby(client);
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

    private static void HandleCreateRoom(ClientState client, string data)
    {
        var roomPayload = JsonConvert.DeserializeObject<Room>(data);
        if (roomPayload == null)
        {
            client.Send(MessageType.Error, "방 생성 데이터 오류");
            return;
        }

        if (rooms.ContainsKey(roomPayload.Name))
        {
            client.Send(MessageType.Error, "이미 존재하는 방 이름입니다.");
            return;
        }

        var room = new Room
        {
            Name = roomPayload.Name,
            IsPrivate = roomPayload.IsPrivate,
            Password = roomPayload.Password ?? "",
            MaxPlayers = roomPayload.MaxPlayers
        };

        room.Players.Add(client);
        rooms.Add(room.Name, room);
        Console.WriteLine($"[방 생성] {room.Name} (공개 여부: {(room.IsPrivate ? "비공개" : "공개")})");
        BroadcastRoomList();
    }

    private static void HandleJoinRoom(ClientState client, string data)
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
            bool swap = new Random().Next(0, 2) == 1;

            foreach (var p in room.Players)
            {
                var gameStartPayload = new { roomName = room.Name, swap = swap };
                string json = JsonConvert.SerializeObject(gameStartPayload);
                p.Send(MessageType.StartGame, json);
            }

            StartItemSpawners(room);
            StartMatchTimer(room, 300); 
        }
    }

    private static void HandleRoomList(ClientState client)
    {
        var summaries = rooms.Values.Select(r => new
        {
            name = r.Name,
            isPrivate = r.IsPrivate,
            current = r.Players.Count,
            max = r.MaxPlayers
        }).ToList();

        string json = JsonConvert.SerializeObject(summaries);
        client.Send(MessageType.RoomList, json);
    }

    private static void BroadcastRoomList()
    {
        foreach (var room in rooms.Values)
        {
            foreach (var player in room.Players.ToArray())
            {
                try { HandleRoomList(player); } catch { }
            }
        }
    }

    private static void HandleMyOrder(ClientState client, string roomName)
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

    private static void RelayMovement(ClientState sender, string data)
    {
        var room = FindRoomOf(sender);
        if (room == null || !room.IsMatchRunning) return; 

        foreach (var player in room.Players)
        {
            if (player != sender)
                player.Send(MessageType.Move, data);
        }
    }

    private static void StartMatchTimer(Room room, int seconds)
    {
        room.MatchCts?.Cancel();
        room.MatchCts?.Dispose();
        room.MatchCts = new CancellationTokenSource();
        room.SecondsLeft = seconds;

        var token = room.MatchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested && room.SecondsLeft > 0)
                {
                    await Task.Delay(1000, token);
                    room.SecondsLeft--;

                    foreach (var p in room.Players)
                        p.Send(MessageType.TimerSync, room.SecondsLeft.ToString());
                }
            }
            catch { /* 취소 등 무시 */ }

            if (token.IsCancellationRequested) return;

            room.DropCts?.Cancel();
            room.DropCts?.Dispose();
            room.DropCts = null;

            int p0 = room.Players.Count > 0 ? room.Players[0].score : 0;
            int p1 = room.Players.Count > 1 ? room.Players[1].score : 0;

            int winner = -1; 
            if (room.Players.Count >= 2)
            {
                if (p0 > p1) winner = 0;
                else if (p1 > p0) winner = 1;
            }

            var payload = new { winner, p0, p1 };
            string json = JsonConvert.SerializeObject(payload);
            foreach (var p in room.Players)
                p.Send(MessageType.MatchOver, json);
        }, token);
    }

    private static void StartItemSpawners(Room room)
    {
        if (dropTable.Count == 0)
        {
            Console.WriteLine("[경고] 드랍테이블 비어있음. 스폰 시작 안함.");
            return;
        }

        room.DropCts?.Cancel();
        room.DropCts?.Dispose();

        room.DropCts = new CancellationTokenSource();
        var token = room.DropCts.Token;

        foreach (var entry in dropTable)
        {
            _ = Task.Run(async () =>
            {
                var rng = new Random(Guid.NewGuid().GetHashCode());
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(entry.dropTime), token);
                    }
                    catch { break; }

                    int qty = rng.Next(entry.dropQuantityMin, entry.dropQuantityMax + 1);
                    if (qty <= 0) continue;

                    for (int i = 0; i < qty; i++)
                    {
                        string instanceId = Guid.NewGuid().ToString();
                        float x = (float)(rng.NextDouble() * (maxX - minX) + minX);
                        float y = (float)(rng.NextDouble() * (maxY - minY) + minY);

                        room.ActiveItemIds.Add(instanceId);
                        room.ItemMap[instanceId] = entry.itemID;

                        var payload = new { instanceId = instanceId, itemId = entry.itemID, x = x, y = y };
                        string json = JsonConvert.SerializeObject(payload);

                        foreach (var p in room.Players)
                            p.Send(MessageType.ItemSpawn, json);

                        Console.WriteLine($"[ItemSpawn] room={room.Name} id={entry.itemID} inst={instanceId} ({x:F2},{y:F2})");
                    }
                }
            }, token);
        }
    }

    private static void HandleItemPickup(ClientState sender, string data)
    {
        string instanceId = data?.Trim() ?? "";
        if (string.IsNullOrEmpty(instanceId)) return;

        var room = FindRoomOf(sender);
        if (room == null || !room.IsMatchRunning) return; 

        int itemId;
        bool picked = false;

        lock (room)
        {
            if (!room.ItemMap.TryGetValue(instanceId, out itemId))
                return;

            if (itemId == 10000 && sender.bag >= 5)
            {
                sender.Send(MessageType.BagUpdate, JsonConvert.SerializeObject(new { bag = sender.bag }));
                return;
            }

            room.ActiveItemIds.Remove(instanceId);
            room.ItemMap.Remove(instanceId);
            picked = true;
        }

        if (!picked) return;

        foreach (var p in room.Players)
            p.Send(MessageType.ItemRemove, instanceId);

        if (itemId == 10000) 
        {
            sender.bag += 1;
            sender.Send(MessageType.BagUpdate, JsonConvert.SerializeObject(new { bag = sender.bag }));
        }
        else
        {
            ApplyItemEffect(room, sender, itemId);
        }
    }

    private static Room? FindRoomOf(ClientState c)
    {
        foreach (var kv in rooms)
        {
            var room = kv.Value;
            if (room.Players.Contains(c)) return room;
        }
        return null;
    }

    private static void ApplyItemEffect(Room room, ClientState target, int itemId)
    {
        if (!itemDefs.TryGetValue(itemId, out var def))
        {
            Console.WriteLine($"[효과 적용 실패] itemId {itemId} 정의 없음");
            return;
        }

        switch (def.effectType)
        {
            case EffectType.Score:
                {
                    target.score += (int)def.value1;

                    int who = room.Players.IndexOf(target);
                    var scorePayload = new { who = who, score = target.score, add = (int)def.value1 };
                    string json = JsonConvert.SerializeObject(scorePayload);
                    foreach (var p in room.Players)
                        p.Send(MessageType.ScoreUpdate, json);

                    Console.WriteLine($"[Score] {target.nickname} +{def.value1} → {target.score}");
                    break;
                }

            case EffectType.PlayerMoveSpeedUp:
                {
                    var buffPayload = new
                    {
                        type = "PlayerMoveSpeedUp",
                        value = def.value1,
                        duration = def.duration
                    };
                    target.Send(MessageType.ApplyBuff, JsonConvert.SerializeObject(buffPayload));
                    Console.WriteLine($"[Buff] {target.nickname} MoveSpeed +{def.value1} for {def.duration}s");
                    break;
                }

            default:
                Console.WriteLine($"[효과 미정의] {def.effectType}");
                break;
        }
    }
    private static void HandleDepositBag(ClientState sender)
    {
        var room = FindRoomOf(sender);
        if (room == null || !room.IsMatchRunning) return; 
        if (sender.bag <= 0) return;

        int add = sender.bag;
        sender.bag = 0;
        sender.score += add;

        sender.Send(MessageType.BagUpdate, JsonConvert.SerializeObject(new { bag = sender.bag }));

        int who = room.Players.IndexOf(sender);
        var payload = new { who = who, score = sender.score, add = add };
        string json = JsonConvert.SerializeObject(payload);
        foreach (var p in room.Players)
            p.Send(MessageType.ScoreUpdate, json);

        Console.WriteLine($"[Deposit] {sender.nickname} 입금 +{add} → score={sender.score}");
    }

    private static void HandleRequestRematch(ClientState client)
    {
        var room = FindRoomOf(client);
        if (room == null) return;

        client.wantsRematch = true;
        Console.WriteLine($"[Rematch] {client.nickname} 요청");

        bool allReady = room.Players.Count == room.MaxPlayers &&
                        room.Players.All(p => p.wantsRematch);

        if (!allReady) return;

        foreach (var p in room.Players)
        {
            p.wantsRematch = false;
            p.score = 0;
            p.bag = 0;
            p.Send(MessageType.BagUpdate, JsonConvert.SerializeObject(new { bag = 0 }));
            var zeroScore = new { who = -1, score = 0, add = 0 };
            p.Send(MessageType.ScoreUpdate, JsonConvert.SerializeObject(zeroScore));
        }

        room.DropCts?.Cancel();
        room.DropCts?.Dispose();
        room.DropCts = null;
        room.ActiveItemIds.Clear();
        room.ItemMap.Clear();

        room.MatchCts?.Cancel();
        room.MatchCts?.Dispose();
        room.MatchCts = null;
        room.SecondsLeft = 0;

        bool swap = new Random().Next(0, 2) == 1;
        foreach (var p in room.Players)
        {
            var gameStartPayload = new { roomName = room.Name, swap = swap };
            string json = JsonConvert.SerializeObject(gameStartPayload);
            p.Send(MessageType.StartGame, json);
        }

        StartItemSpawners(room);
        StartMatchTimer(room, 300); 
        Console.WriteLine($"[Rematch] 방 {room.Name} 재시작");
    }
    private static void HandleExitToLobby(ClientState client)
    {
        var room = FindRoomOf(client);
        if (room == null) return;

        Console.WriteLine($"[Exit] {client.nickname} → 방 {room.Name} 해체 후 로비로");

        foreach (var p in room.Players.ToArray())
        {
            try { p.Send(MessageType.ReturnToLobby, ""); } catch { }
        }

        room.DropCts?.Cancel();
        room.DropCts?.Dispose();
        room.DropCts = null;

        room.MatchCts?.Cancel();
        room.MatchCts?.Dispose();
        room.MatchCts = null;
        room.SecondsLeft = 0;

        room.ActiveItemIds.Clear();
        room.ItemMap.Clear();

        rooms.Remove(room.Name);
        BroadcastRoomList();
    }
    private static void CleanupClient(ClientState client)
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
                {
                    roomsToRemove.Add(room.Name);

                    room.DropCts?.Cancel();
                    room.DropCts?.Dispose();
                    room.DropCts = null;

                    room.MatchCts?.Cancel();
                    room.MatchCts?.Dispose();
                    room.MatchCts = null;
                    room.SecondsLeft = 0;

                    room.ActiveItemIds.Clear();
                    room.ItemMap.Clear();
                }
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
    public class ClientState
    {
        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        public string id = Guid.NewGuid().ToString();
        public string nickname = "";
        public int score = 0;
        public int bag = 0;
        public bool wantsRematch = false;

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
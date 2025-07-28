using UnityEngine;
using UnityEngine.UI;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.SceneManagement;

public class UnityClient : MonoBehaviour
{
    [Header("패널들")]
    public GameObject createRoomPanel;
    public GameObject joinRoomPanel;
    public GameObject mainLobbyPanel;

    [Header("Create Room UI")]
    public InputField roomNameInput;
    public Dropdown isPrivateDropdown;
    public InputField passwordInput;
    public Dropdown maxPlayersDropdown;
    public Button createButton;
    public Button cancelCreateButton;

    [Header("Join Room UI")]
    public InputField joinRoomNameInput;
    public InputField joinPasswordInput;
    public Button joinButton;
    public Button cancelJoinButton;

    [Header("메인 로비 UI")]
    public Button openCreatePopupButton;
    public Button openJoinPopupButton;

    private TcpClient client;
    private NetworkStream stream;
    private bool isConnected = false;

    private byte[] buffer = new byte[2048];
    private StringBuilder incomingData = new();

    private bool connectionErrorLogged = false;

    void Start()
    {
        ConnectToServer();

        createButton.onClick.AddListener(CreateRoom);
        joinButton.onClick.AddListener(JoinRoom);
        isPrivateDropdown.onValueChanged.AddListener(OnPrivacyChanged);

        openCreatePopupButton.onClick.AddListener(() => ShowPanel(createRoomPanel));
        openJoinPopupButton.onClick.AddListener(() => ShowPanel(joinRoomPanel));
        cancelCreateButton.onClick.AddListener(() => ShowPanel(mainLobbyPanel));
        cancelJoinButton.onClick.AddListener(() => ShowPanel(mainLobbyPanel));

        ShowPanel(mainLobbyPanel);
        passwordInput.gameObject.SetActive(false);
    }

    void ShowPanel(GameObject panel)
    {
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        mainLobbyPanel.SetActive(false);
        panel.SetActive(true);
    }

    void OnPrivacyChanged(int index)
    {
        passwordInput.gameObject.SetActive(index == 1);
    }

    void ConnectToServer()
    {
        try
        {
            client = new TcpClient("127.0.0.1", 7777);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("서버 연결 성공");

            Thread t = new Thread(ReceiveLoop);
            t.IsBackground = true;
            t.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"서버 연결 실패: {e.Message}");
        }
    }

    void CreateRoom()
    {
        if (!isConnected) return;

        var room = new RoomData
        {
            Name = roomNameInput.text,
            IsPrivate = isPrivateDropdown.value == 1,
            Password = passwordInput.text,
            MaxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text)
        };

        SendToServer(new GameMessage
        {
            Type = MessageType.CreateRoom,
            Data = JsonUtility.ToJson(room)
        });
    }

    void JoinRoom()
    {
        if (!isConnected) return;

        var joinData = new JoinRoomRequest
        {
            roomName = joinRoomNameInput.text,
            password = joinPasswordInput.text
        };

        SendToServer(new GameMessage
        {
            Type = MessageType.JoinRoom,
            Data = JsonUtility.ToJson(joinData)
        });
    }

    void SendToServer(GameMessage msg)
    {
        try
        {
            string json = JsonUtility.ToJson(msg);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            stream.Write(data, 0, data.Length);
            Debug.Log($"[보냄] {json}");
        }
        catch (Exception e)
        {
            Debug.LogError("서버 전송 실패: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        while (isConnected)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0) continue;

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Debug.Log($"[수신] {json}");

                GameMessage msg = JsonUtility.FromJson<GameMessage>(json);

                switch (msg.Type)
                {
                    case MessageType.StartGame:
                        Debug.Log("매칭 완료, 게임 시작!");
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            SceneManager.LoadScene("GameScene");
                        });
                        break;

                    case MessageType.Error:
                        Debug.LogWarning("서버 에러: " + msg.Data);
                        break;

                    default:
                        Debug.Log("알 수 없는 메시지: " + msg.Type);
                        break;
                }
            }
            catch (Exception)
            {
                if (isConnected)
                {
                    Debug.LogWarning("서버와의 연결이 종료되었습니다.");
                    isConnected = false;
                }
            }
        }
    }

    [Serializable]
    public class RoomData
    {
        public string Name;
        public bool IsPrivate;
        public string Password;
        public int MaxPlayers;
    }

    [Serializable]
    public class JoinRoomRequest
    {
        public string roomName;
        public string password;
    }

    [Serializable]
    public class GameMessage
    {
        public MessageType Type;
        public string Data;
    }

    public enum MessageType { CreateRoom, JoinRoom, StartGame, RoomList, Error }
}
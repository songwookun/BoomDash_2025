using UnityEngine;
using UnityEngine.UI;

public class LobbyUIBinder : MonoBehaviour
{
    [Header("Panels")]
    public GameObject createRoomPanel;
    public GameObject joinRoomPanel;
    public GameObject mainLobbyPanel;

    [Header("Create UI")]
    public InputField roomNameInput;
    public Dropdown isPrivateDropdown;
    public InputField passwordInput;
    public Dropdown maxPlayersDropdown;
    public Button createButton;
    public Button cancelCreateButton;

    [Header("Join UI")]
    public InputField joinRoomNameInput;
    public InputField joinPasswordInput;
    public Button joinButton;
    public Button cancelJoinButton;

    [Header("Open Popup Buttons")]
    public Button openCreatePopupButton;
    public Button openJoinPopupButton;

    private void Awake()
    {
        if (UnityClient.Instance != null)
        {
            UnityClient.Instance.BindLobbyUI(this); // 씬 로드시 즉시 재바인딩
        }
    }
}

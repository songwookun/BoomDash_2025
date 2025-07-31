using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject player1Prefab;
    public GameObject player2Prefab;
    public Transform[] spawnPoints;

    private GameObject myPlayer;
    private GameObject otherPlayer;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnPlayers(int myIndex)
    {
        Debug.Log($"[GameManager] 플레이어 스폰 - 내 인덱스: {myIndex}");

        int otherIndex = 1 - myIndex;

        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        myPlayer = Instantiate(myPrefab, spawnPoints[myIndex].position, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isMine = true;

        otherPlayer = Instantiate(otherPrefab, spawnPoints[otherIndex].position, Quaternion.identity);
        otherPlayer.GetComponent<PlayerController>().isMine = false;
    }

    public void UpdateOtherPlayerPosition(float x, float y)
    {
        if (otherPlayer != null)
        {
            otherPlayer.GetComponent<PlayerController>().SetPosition(new Vector3(x, y, 0));
        }
    }
}

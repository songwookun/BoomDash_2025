using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject player1Prefab;
    public GameObject player2Prefab;
    public GameObject areaPrefab;
    public GameObject area2Prefab;

    private GameObject myPlayer;
    private GameObject otherPlayer;

    private Transform areaPos;
    private Transform area2Pos;

    void Awake()
    {
        Instance = this;
        PlayerStatLoader.LoadStats();
    }

    public void SpawnPlayers(int myIndex, bool swap)
    {
        Debug.Log($"[GameManager] 플레이어 스폰 - 내 인덱스: {myIndex}, swap: {swap}");

        Vector3 areaSpawnPos_LB = new Vector3(-8, -4, 0);
        Vector3 areaSpawnPos_RT = new Vector3(8, 4, 0);

        GameObject areaObj = Instantiate(areaPrefab, swap ? areaSpawnPos_LB : areaSpawnPos_RT, Quaternion.identity);
        GameObject area2Obj = Instantiate(area2Prefab, swap ? areaSpawnPos_RT : areaSpawnPos_LB, Quaternion.identity);

        areaPos = areaObj.transform;
        area2Pos = area2Obj.transform;

        Vector3 GetInnerSpawn(Transform areaTransform)
        {
            float radius = 0.3f;
            float offsetX = Random.Range(-radius, radius);
            float offsetY = Random.Range(-radius, radius);
            return areaTransform.position + new Vector3(offsetX, offsetY, 0);
        }

        int otherIndex = 1 - myIndex;
        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        Vector3 mySpawn = myIndex == 0 ? GetInnerSpawn(areaPos) : GetInnerSpawn(area2Pos);
        Vector3 otherSpawn = myIndex == 0 ? GetInnerSpawn(area2Pos) : GetInnerSpawn(areaPos);

        myPlayer = Instantiate(myPrefab, mySpawn, Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isMine = true;

        otherPlayer = Instantiate(otherPrefab, otherSpawn, Quaternion.identity);
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

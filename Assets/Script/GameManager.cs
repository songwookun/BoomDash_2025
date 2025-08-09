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

        Vector3 posLB = new Vector3(-8, -4, 0);
        Vector3 posRT = new Vector3(8, 4, 0);

        GameObject area1 = Instantiate(areaPrefab, posRT, Quaternion.identity); 
        GameObject area2 = Instantiate(area2Prefab, posLB, Quaternion.identity); 

        Transform areaRT = area1.transform;
        Transform areaLB = area2.transform;

        Transform myArea, otherArea;
        if (myIndex == 0) 
        {
            myArea = swap ? areaRT : areaLB;
            otherArea = swap ? areaLB : areaRT;
        }
        else 
        {
            myArea = swap ? areaLB : areaRT;
            otherArea = swap ? areaRT : areaLB;
        }

        Vector3 GetInnerSpawn(Transform areaTransform)
        {
            float radius = 0.3f;
            float offsetX = Random.Range(-radius, radius);
            float offsetY = Random.Range(-radius, radius);
            return areaTransform.position + new Vector3(offsetX, offsetY, 0);
        }

        GameObject myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        GameObject otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        myPlayer = Instantiate(myPrefab, GetInnerSpawn(myArea), Quaternion.identity);
        myPlayer.GetComponent<PlayerController>().isMine = true;

        otherPlayer = Instantiate(otherPrefab, GetInnerSpawn(otherArea), Quaternion.identity);
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

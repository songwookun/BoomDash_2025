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
        var posLB = new Vector3(-8, -4, 0);
        var posRT = new Vector3(8, 4, 0);

        var area1 = Instantiate(areaPrefab, posRT, Quaternion.identity); 
        var area2 = Instantiate(area2Prefab, posLB, Quaternion.identity); 

        var rtBounds = area1.GetComponentInChildren<Collider2D>().bounds;
        var lbBounds = area2.GetComponentInChildren<Collider2D>().bounds;

        Transform myArea, otherArea;
        if (myIndex == 0)
        { 
            myArea = swap ? area1.transform : area2.transform;
            otherArea = swap ? area2.transform : area1.transform;
        }
        else
        {    
            myArea = swap ? area2.transform : area1.transform;
            otherArea = swap ? area1.transform : area2.transform;
        }

        Bounds areaOf(Transform t) => (t == area1.transform) ? rtBounds : lbBounds;

        Vector3 RandInside(Bounds b)
        {
            float x = Random.Range(b.min.x + 0.2f, b.max.x - 0.2f);
            float y = Random.Range(b.min.y + 0.2f, b.max.y - 0.2f);
            return new Vector3(x, y, 0);
        }

        var myPrefab = myIndex == 0 ? player1Prefab : player2Prefab;
        var otherPrefab = myIndex == 0 ? player2Prefab : player1Prefab;

        var myPos = RandInside(areaOf(myArea));
        var otherPos = RandInside(areaOf(otherArea));

        myPlayer = Instantiate(myPrefab, myPos, Quaternion.identity);
        var myPC = myPlayer.GetComponent<PlayerController>();
        myPC.isMine = true;
        myPC.SetForbiddenBounds(areaOf(otherArea)); 

        otherPlayer = Instantiate(otherPrefab, otherPos, Quaternion.identity);
        var oPC = otherPlayer.GetComponent<PlayerController>();
        oPC.isMine = false;
        oPC.SetForbiddenBounds(areaOf(myArea));     
    }



    public void UpdateOtherPlayerPosition(float x, float y)
    {
        if (otherPlayer != null)
        {
            otherPlayer.GetComponent<PlayerController>().SetPosition(new Vector3(x, y, 0));
        }
    }
}

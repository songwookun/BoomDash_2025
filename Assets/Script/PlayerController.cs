using UnityEngine;
using Newtonsoft.Json;

public class PlayerController : MonoBehaviour
{
    public bool isMine = false;
    public int playerId = 0;
    private float speed;
    void Start()
    {
        speed = PlayerStatLoader.GetMoveSpeed(playerId);
        Debug.Log($"[PlayerController] ID {playerId} 이동속도: {speed}");
    }

    void Update()
    {
        if (!isMine) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, v, 0);

        if (dir.magnitude > 0.01f)
        {
            transform.position += dir * speed * Time.deltaTime;
            UnityClient.Instance.SendMove(transform.position.x, transform.position.y);
        }
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }
}
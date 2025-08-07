using UnityEngine;
using Newtonsoft.Json;

public class PlayerController : MonoBehaviour
{
    public bool isMine = false;
    public int playerId = 0;
    private float speed;
    private float minX, maxX, minY, maxY;

    void Start()
    {
        speed = PlayerStatLoader.GetMoveSpeed(playerId);
        Debug.Log($"[PlayerController] ID {playerId} 이동속도: {speed}");

        Camera cam = Camera.main;
        float vertExtent = cam.orthographicSize;
        float horzExtent = vertExtent * cam.aspect;
        Vector3 camPos = cam.transform.position;

        minX = camPos.x - horzExtent;
        maxX = camPos.x + horzExtent;
        minY = camPos.y - vertExtent;
        maxY = camPos.y + vertExtent;
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
            Vector3 clampedPos = transform.position;
            clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
            clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);
            transform.position = clampedPos;

            UnityClient.Instance.SendMove(transform.position.x, transform.position.y);
        }
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }
}

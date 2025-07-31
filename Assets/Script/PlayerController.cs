using UnityEngine;
using Newtonsoft.Json;

public class PlayerController : MonoBehaviour
{
    public bool isMine = false;
    public float speed = 5f;

    void Update()
    {
        if (!isMine) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, v, 0);

        if (dir.magnitude > 0.01f)
        {
            transform.position += dir * speed * Time.deltaTime;

            Vector3 pos = transform.position;
            UnityClient.Instance.SendMove(pos.x, pos.y);
        }
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }
}
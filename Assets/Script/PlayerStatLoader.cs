using System.Collections.Generic;
using UnityEngine;
using System.IO;

public static class PlayerStatLoader
{
    private static Dictionary<int, float> moveSpeeds = new Dictionary<int, float>();

    public static void LoadStats()
    {
        moveSpeeds.Clear();

        TextAsset csvFile = Resources.Load<TextAsset>("Data/PlayerStat");
        if (csvFile == null)
        {
            Debug.LogError("[PlayerStatLoader] CSV ������ ã�� �� �����ϴ�.");
            return;
        }

        using (StringReader reader = new StringReader(csvFile.text))
        {
            string line;
            bool isFirst = true;

            while ((line = reader.ReadLine()) != null)
            {
                if (isFirst) { isFirst = false; continue; } 
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split(',');
                if (parts.Length < 2) continue;

                if (int.TryParse(parts[0], out int id) &&
                    float.TryParse(parts[1], out float moveSpeed))
                {
                    moveSpeeds[id] = moveSpeed;
                }
            }
        }

        Debug.Log("[PlayerStatLoader] �ε� �Ϸ�: " + moveSpeeds.Count + "��");
    }

    public static float GetMoveSpeed(int playerId)
    {
        return moveSpeeds.TryGetValue(playerId, out float speed) ? speed : 1f; 
    }
}

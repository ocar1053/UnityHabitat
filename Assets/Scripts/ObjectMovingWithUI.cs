using UnityEngine;
using TMPro;
using System.Collections;

public class ObjectMovingWithUI : MonoBehaviour
{
    public Transform[] objects;
    public TextMeshPro[] textMeshes;
    public Vector3 moveDirection = Vector3.right;
    public float stepDistance = 1f;
    public float stepDuration = 5f;
    public int stepCount = 5;

    private Vector3[] startPositions;

    void Start()
    {
        startPositions = new Vector3[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                startPositions[i] = objects[i].position;
            }
        }
        StartCoroutine(MoveObjects());
    }

    IEnumerator MoveObjects()
    {
        while (true)
        {

            for (int step = 0; step < stepCount; step++)
            {
                yield return MoveStep(stepDistance);
            }


            for (int step = 0; step < stepCount; step++)
            {
                yield return MoveStep(-stepDistance);
            }
        }
    }

    IEnumerator MoveStep(float distance)
    {
        float elapsedTime = 0;
        Vector3[] startPos = new Vector3[objects.Length];
        Vector3[] targetPos = new Vector3[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                startPos[i] = objects[i].position;
                targetPos[i] = startPos[i] + moveDirection * distance;
            }
        }

        while (elapsedTime < stepDuration)
        {
            float t = elapsedTime / stepDuration;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].position = Vector3.Lerp(startPos[i], targetPos[i], t);


                    if (textMeshes[i] != null)
                    {
                        textMeshes[i].text = $"({objects[i].position.x:F2}, {objects[i].position.y:F2}, {objects[i].position.z:F2})";
                        textMeshes[i].transform.position = objects[i].position + new Vector3(-1, 0.8f, 0);
                        textMeshes[i].transform.LookAt(Camera.main.transform);
                        textMeshes[i].transform.Rotate(0, 180, 0);
                    }
                }
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }


        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                objects[i].position = targetPos[i];
            }
        }
    }
}

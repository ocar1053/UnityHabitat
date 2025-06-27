using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 定義一組位置
[System.Serializable]
public class PositionGroup
{
    public string groupName;            // 組別名稱（可選）
    public List<Transform> positions;   // 這組的所有錨點（拖空物件進來）
}

public class objectMover : MonoBehaviour
{
    [Header("要移動的物體")]
    public List<GameObject> objectsToMove;

    [Header("所有位置組別")]
    public List<PositionGroup> positionGroups;

    [Header("預設切換到哪一組")]
    public int currentGroupIndex = 0;

    void Start()
    {
        // 啟動時先應用一次預設組
        ApplyGroup(currentGroupIndex);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
            SwitchGroup(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            SwitchGroup(-1);
    }

    // 切換並套用新組
    void SwitchGroup(int dir)
    {
        if (positionGroups == null || positionGroups.Count == 0)
            return;

        currentGroupIndex = (currentGroupIndex + dir + positionGroups.Count) % positionGroups.Count;
        ApplyGroup(currentGroupIndex);
        Debug.Log($"切換到組別 [{currentGroupIndex}] {positionGroups[currentGroupIndex].groupName}");
    }

    // 把物體移到該組對應的錨點
    void ApplyGroup(int groupIndex)
    {
        var pts = positionGroups[groupIndex].positions;
        int count = Mathf.Min(objectsToMove.Count, pts.Count);
        for (int i = 0; i < count; i++)
        {
            var obj = objectsToMove[i];
            var t   = pts[i];
            obj.transform.position = t.position;
            obj.transform.rotation = t.rotation;
        }
    }
}

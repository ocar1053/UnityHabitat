using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class TargetGroup {
    [Tooltip("一組目標點，需與要移動的物件數量對應")]
    public List<Transform> targets;
}

public class MoveOnKeyPress : MonoBehaviour {
    [Header("要移動的固定物件 (可任意數量)")]
    [Tooltip("從頭到尾都相同的物件列表")]
    public List<Transform> objectsToMove;

    [Header("多組目標點設定")]  
    [Tooltip("在此列表中設定多組目標點，每組包含與 objectsToMove 對應數量的 Transform")]
    public List<TargetGroup> targetGroups;

    [Header("移動參數")]
    [Tooltip("移動持續時間 (秒)")]
    public float moveDuration = 3f;

    [Header("按鍵設定")]
    [Tooltip("按下此鍵啟動移動")]
    public KeyCode activationKey = KeyCode.S;

    void Update() {
        // 用數字鍵 1~9 切換目標組別
        for (int i = 0; i < targetGroups.Count && i < 9; i++) {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) {
                Debug.Log($"[MoveOnKeyPress] 切換到目標組: {i+1}");
                groupIndex = i+1;
            }
        }

        // 按 activationKey 啟動移動
        if (Input.GetKeyDown(activationKey)) {
            Debug.Log($"[MoveOnKeyPress] 按下 {activationKey}，開始將 {objectsToMove.Count} 個物件移動到組 {groupIndex}");
            // 檢查
            if (objectsToMove == null || objectsToMove.Count == 0) {
                Debug.LogWarning("未設定要移動的物件列表");
                return;
            }
            if (targetGroups == null || targetGroups.Count == 0) {
                Debug.LogWarning("未設定任何目標點組");
                return;
            }
            if (groupIndex < 0 || groupIndex >= targetGroups.Count) {
                Debug.LogError($"目標組索引錯誤: groupIndex={groupIndex}");
                return;
            }
            var selected = targetGroups[groupIndex];
            if (selected.targets == null || selected.targets.Count != objectsToMove.Count) {
                Debug.LogError($"第 {groupIndex} 組目標點數量 ({selected.targets?.Count ?? 0}) 與物件數量 ({objectsToMove.Count}) 不符");
                return;
            }
            StartCoroutine(MoveObjectsToTargets(objectsToMove, selected.targets, moveDuration));
        }
    }

    // 匯出為 Inspector 可見，用於查看當前目標組索引
    [Header("(非必須) 目前選擇目標組索引")]  
    [Tooltip("僅供查看，可透過數字鍵切換")]
    public int groupIndex = 0;

    private IEnumerator MoveObjectsToTargets(List<Transform> objects, List<Transform> targets, float duration) {
        int count = objects.Count;
        duration = Mathf.Max(0.01f, duration);
        Vector3[] starts = new Vector3[count];
        float[] speeds = new float[count];
        for (int i = 0; i < count; i++) {
            starts[i] = objects[i].position;
            float dist = Vector3.Distance(starts[i], targets[i].position);
            speeds[i] = dist / duration;
        }
        bool finished = false;
        while (!finished) {
            finished = true;
            for (int i = 0; i < count; i++) {
                objects[i].position = Vector3.MoveTowards(objects[i].position, targets[i].position, speeds[i] * Time.deltaTime);
                if (Vector3.Distance(objects[i].position, targets[i].position) > 0.001f) finished = false;
            }
            yield return null;
        }
    }
}

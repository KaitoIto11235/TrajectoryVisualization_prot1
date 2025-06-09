using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 円錐を2点間（モデルデータの0番目と1番目の点）に配置・回転させるスクリプト
/// </summary>
public class ConeAligner : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("デバッグ用に点と線を表示")]
    public bool showDebugVisuals = true;

    // FileOperationへの参照
    private FileOperation fileOperation;
    // 有効なデータがロードされているか
    private bool hasValidPoints = false;

    // FileOperationがロードされるまで待機する最大時間（秒）
    public float maxWaitTime = 5.0f;
    private float waitTimer = 0f;
    private bool isInitialized = false;

    [SerializeField] GameObject Cone;
    [SerializeField] Material coneMaterial;

    void Start()
    {
        // 即時に初期化を試みる
        TryInitialize();
    }

    void Update()
    {
        // まだ初期化されていない場合は初期化を試みる
        if (!isInitialized)
        {
            // 最大待機時間を超えていなければ初期化を再試行
            if (waitTimer < maxWaitTime)
            {
                waitTimer += Time.deltaTime;
                TryInitialize();
            }
            else if (waitTimer >= maxWaitTime)
            {
                Debug.LogWarning($"ConeAligner: {maxWaitTime}秒間FileOperationの初期化を待ちましたが、modelPositionsを取得できませんでした。");
                // 待機終了（エラーメッセージが大量に出るのを防ぐ）
                isInitialized = true;
            }
            return;
        }

        
        
    }


        // FileOperationとmodelPositionsの初期化を試みる
    private void TryInitialize()
    {
        try
        {
            // AutoConditionからFileOperationを取得
            if (fileOperation == null)
            {
                if (TrajToAutoCondition.trajToAutoFile != null)
                {
                    fileOperation = TrajToAutoCondition.trajToAutoFile;
                }
                else
                {
                    // まだTrajeToAutoCondition.trajToAutoFileが初期化されていない
                    return;
                }
            }
            
            // FileOperationのmodelPositionsが初期化され、十分なデータがあるか確認
            if (fileOperation.modelPositions != null)
            {
                for(int trajectoryIndex = 0; trajectoryIndex <= 710; trajectoryIndex += 9)
                {
                    Vector3 startPoint = fileOperation.modelPositions[trajectoryIndex];
                    Vector3 endPoint = fileOperation.modelPositions[trajectoryIndex + 9];
                    hasValidPoints = true;
                    isInitialized = true;
                    
                    // 円錐を配置
                    AlignCone(startPoint, endPoint, trajectoryIndex/9);
                }
                Debug.Log("ConeAligner: 正常に初期化され、円錐を配置しました。");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ConeAligner初期化中にエラーが発生: {e.Message}");
            // エラーが発生しても初期化プロセス自体は続行
        }
    }

    /// <summary>
    /// 円錐を2点間に合わせて配置・回転させる
    /// </summary>
    public void AlignCone(Vector3 startPoint, Vector3 endPoint, int coneNumber)
    {
        // 有効なポイントがない場合は何もしない
        if (!hasValidPoints)
        {
            return;
        }

        // 始点と終点が一致する場合は処理しない
        if (startPoint == endPoint)
        {
            Debug.LogWarning("始点と終点が同じ位置に設定されています。");
            return;
        }


        // 方向ベクトルを計算
        Vector3 direction = endPoint - startPoint;
        float distance = direction.magnitude;

        GameObject cone = Instantiate(Cone, (startPoint + endPoint) * 0.5f, Quaternion.FromToRotation(Vector3.forward, direction));
        cone.tag = "Trajectory";
        // 円錐のスケールを調整（距離に応じてY軸方向に拡大）
        cone.transform.localScale = new Vector3(1.0f, 1.0f, distance * 50f);
        Material mat = new Material(coneMaterial);
        cone.GetComponent<Renderer>().material = mat;
        float ratency = coneNumber; 
        ratency /= 10f;
        mat.SetFloat("_Ratency", ratency);
    }

    
}

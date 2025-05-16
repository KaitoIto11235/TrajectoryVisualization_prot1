using UnityEngine;
using System.Collections.Generic;

public class TransparencyManager
{
    //private string targetTag = "TransparentTarget"; // 透明化対象のタグ
    private float alphaValue = 0f; // 透明度 (0: 完全透明, 1: 不透明)
    
    // カスタムされた条件に基づいて透明化するデリゲート
    public delegate bool TransparencyCondition(GameObject obj);
    
    // レンダラーごとのオリジナルマテリアル情報を保存
    private Dictionary<Renderer, MaterialData> originalMaterials = new Dictionary<Renderer, MaterialData>();
    private List<GameObject> affectedObjectsA = new List<GameObject>(); // タグA用
    private List<GameObject> affectedObjectsB = new List<GameObject>(); // タグB用

    // マテリアルデータを保存するための構造体
    private struct MaterialData
    {
        public Material[] originalMaterials;
        public Color[] originalColors;
    }

    private void Start()
    {
        // 必要に応じてここで初期設定
    }

    /// <summary>
    /// 特定のタグを持つすべてのオブジェクトを透明化します
    /// </summary>
    public void MakeTaggedObjectsTransparent(string targetTag)
    {
        if (string.IsNullOrEmpty(targetTag)) return;
        
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(targetTag);
        foreach (GameObject obj in taggedObjects)
        {
            MakeObjectTransparent(obj, alphaValue);
            if (targetTag == "Trajectory" && !affectedObjectsA.Contains(obj))
            {
                affectedObjectsA.Add(obj);
            }
            else if (targetTag == "AutoModel" && !affectedObjectsB.Contains(obj))
            {
                affectedObjectsB.Add(obj);
            }
        }
    }

    /// <summary>
    /// 単一のオブジェクトとその子オブジェクトを透明化します
    /// </summary>
    /// <param name="obj">透明化するオブジェクト</param>
    /// <param name="alpha">適用する透明度 (0-1)</param>
    public void MakeObjectTransparent(GameObject obj, float alpha)
    {
        // レンダラーコンポーネントを取得（自身と子オブジェクト含む）
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        
        foreach (Renderer renderer in renderers)
        {
            // オリジナルのマテリアルを保存（まだ保存されていない場合）
            if (!originalMaterials.ContainsKey(renderer))
            {
                Material[] originalMats = renderer.materials;
                MaterialData data = new MaterialData
                {
                    originalMaterials = new Material[originalMats.Length],
                    originalColors = new Color[originalMats.Length]
                };
                
                // マテリアルのインスタンスとカラーを保存
                for (int i = 0; i < originalMats.Length; i++)
                {
                    // マテリアルのコピーを作成
                    data.originalMaterials[i] = new Material(originalMats[i]);
                    
                    if (originalMats[i].HasProperty("_BaseColor"))
                    {
                        data.originalColors[i] = originalMats[i].GetColor("_BaseColor");
                    }
                    else if (originalMats[i].HasProperty("_Color"))
                    {
                        data.originalColors[i] = originalMats[i].color;
                    }
                    else
                    {
                        data.originalColors[i] = Color.white;
                    }
                }
                
                originalMaterials.Add(renderer, data);
            }
            
            // マテリアルを取得
            Material[] materials = renderer.materials;
            
            // 各マテリアルの透明度を設定
            for (int i = 0; i < materials.Length; i++)
            {
                // シェーダーがStandardまたはURPのLitシェーダーである場合の処理
                if (materials[i].HasProperty("_BaseColor"))
                {
                    // URP/HDRP
                    Color color = materials[i].GetColor("_BaseColor");
                    color.a = alpha;
                    materials[i].SetColor("_BaseColor", color);
                }
                else if (materials[i].HasProperty("_Color"))
                {
                    // Standard
                    Color color = materials[i].color;
                    color.a = alpha;
                    materials[i].color = color;
                }
                
                // 透明度を有効にする
                SetupTransparency(materials[i]);
            }
            
            // 変更を適用
            renderer.materials = materials;
        }
    }

    /// <summary>
    /// マテリアルの透明度を設定するためのシェーダー設定を行います
    /// </summary>
    /// <param name="material">設定するマテリアル</param>
    private void SetupTransparency(Material material)
    {
        // レンダリングモードを設定
        if (material.HasProperty("_SrcBlend") && material.HasProperty("_DstBlend"))
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        
        // Z書き込みを設定
        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }
        
        // レンダリングモードをフェードに設定
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 2); // 2 = Fade
        }
        
        // 透明度を有効にする
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        // シェーダーキーワード設定
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    /// <summary>
    /// 特定のタグを持つオブジェクトを元の状態に戻します
    /// </summary>
    /// <param name="tag">元に戻すオブジェクトのタグ</param>
    public void RestoreObjects(string tag)
    {
        List<GameObject> affectedObjectsToRestore = tag == "Trajectory" ? affectedObjectsA : affectedObjectsB;

        foreach (GameObject obj in affectedObjectsToRestore)
        {
            if (obj != null)
            {
                RestoreObject(obj);
            }
        }
        
        if (tag == "Trajectory")
        {
            affectedObjectsA.Clear();
        }
        else if (tag == "AutoModel")
        {
            affectedObjectsB.Clear();
        }
    }

    /// <summary>
    /// 単一のオブジェクトを元の状態に戻します
    /// </summary>
    /// <param name="obj">元に戻すオブジェクト</param>
    public void RestoreObject(GameObject obj)
    {
        if (obj == null) return;
        
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        
        foreach (Renderer renderer in renderers)
        {
            if (originalMaterials.ContainsKey(renderer))
            {
                MaterialData data = originalMaterials[renderer];
                
                // 新しいマテリアル配列を作成
                Material[] newMaterials = new Material[data.originalMaterials.Length];
                
                // 各マテリアルを元に戻す
                for (int i = 0; i < data.originalMaterials.Length; i++)
                {
                    // 保存していたマテリアルのコピーを作成
                    newMaterials[i] = new Material(data.originalMaterials[i]);
                    
                    // 色を元に戻す
                    if (newMaterials[i].HasProperty("_BaseColor"))
                    {
                        newMaterials[i].SetColor("_BaseColor", data.originalColors[i]);
                    }
                    else if (newMaterials[i].HasProperty("_Color"))
                    {
                        newMaterials[i].color = data.originalColors[i];
                    }
                }
                
                // 新しいマテリアル配列を適用
                renderer.materials = newMaterials;
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;

public class ControllerConfig : MonoBehaviour
{
    private Vector3 RightHandPosition;
    private Quaternion RightHandRotationQ;
    [SerializeField] GameObject controllerSphere, offsetSphere;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 newRightHandPosition))
        {
            RightHandPosition = newRightHandPosition;
        }
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion newRightHandRotationQ))
        {
            RightHandRotationQ = newRightHandRotationQ;
        }

        controllerSphere.transform.position = RightHandPosition;
        offsetSphere.transform.position = ControllerOffset(RightHandPosition, RightHandRotationQ);
    }

    public Vector3 ControllerOffset(Vector3 pos, Quaternion rotQ)
    {
        // コントローラの前方向を計算
        Vector3 forward = rotQ * Vector3.forward;
        // コントローラの下方向を計算
        Vector3 down = rotQ * Vector3.down;
        // コントローラの左方向を計算
        Vector3 left = rotQ * Vector3.left;

        // シフト方向を決める重み
        float downWeight = 0.2f;
        float leftWeight = 0.1f;
        Vector3 dir = down * downWeight + left * leftWeight + forward * (1f - downWeight - leftWeight);
        
        // 前方向に沿って少し手前にオフセット
        float offsetDistance = -0.2f; // 手前に移動する距離（調整可能）
        Vector3 offset = dir * offsetDistance;
        
        return pos + offset;
    }
}

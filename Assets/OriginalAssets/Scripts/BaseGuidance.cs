using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Text;
using UnityEngine.XR;
using Valve.VR;

public interface IGuidance
{
    float Evaluation();
    void Moving(int updateCount);
    void GuidanceUpdate();
}

public abstract class BaseGuidance : IGuidance
{
    protected GameObject user, guidance;
    protected int fileRowCount;
    protected Vector3[] modelPositions;
    public Vector3[] ModelPositions
    {
        get {return modelPositions;}
    }
    protected Quaternion[] modelQuaternions;
    public Quaternion[] ModelQuaternions
    {
        get {return modelQuaternions;}
    }
    protected Material[] materialArray;

    public BaseGuidance(GameObject guidance, GameObject user, int fileRowCount, Vector3[] positions, Quaternion[] quaternions, Material[] materialArray)
    {
        this.guidance = guidance;
        this.user = user;
        this.fileRowCount = fileRowCount;
        this.modelPositions = new Vector3[fileRowCount];
        this.modelPositions = positions;
        this.modelQuaternions = new Quaternion[fileRowCount];
        this.modelQuaternions = quaternions;
        this.materialArray = materialArray;
    }

    public abstract float Evaluation();
    public abstract void Moving(int updateCount);
    public abstract void GuidanceUpdate();
}


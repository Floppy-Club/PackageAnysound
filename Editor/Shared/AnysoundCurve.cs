using System;
using UnityEngine;

[Serializable]
public class AnysoundCurve
{
    public AnimationCurve curve;

    public AnysoundCurve(AnimationCurve curve)
    {
        this.curve = curve;
    }
}
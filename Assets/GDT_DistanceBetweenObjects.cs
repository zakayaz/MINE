using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GDT_DistanceBetweenObjects : MonoBehaviour
{
    public TMP_Text textDistance;

    public GameObject pointA;
    public GameObject pointB;

    void Start()
    {
        
    }

    void Update()
    {
        if (pointA == null || pointB == null)
        {
            textDistance.text = "Missing object!";
            return;
        }

        float distance = Vector3.Distance(pointA.transform.position, pointB.transform.position);

        textDistance.text = "Distance: " + distance.ToString("F2") + " m";
    }
}
using UnityEngine;

public class FollowCamera1 : MonoBehaviour
{
    public GameObject player;
    private Vector3 localOffset;

    void Start()
    {
        // Offset calculé en espace local du joueur (tient compte de sa rotation)
        localOffset = player.transform.InverseTransformPoint(transform.position);
    }

    void LateUpdate()
    {
        // Suit la position ET la rotation du joueur
        transform.position = player.transform.TransformPoint(localOffset);
        transform.rotation = player.transform.rotation;
    }
}
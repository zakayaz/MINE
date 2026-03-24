using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Globalization;

public class GPSIMUPlayer : MonoBehaviour
{
    [Header("References")]
    public GameObject vehicleObject;

    [Header("CSV File")]
    public string csvFileName = "GPS_IMU_DATA.csv";

    [Header("Playback")]
    public float playbackSpeed = 1.0f;
    public float positionScale = 1.0f;
    public bool loop = true;
    public bool drawPath = true;
    public bool smoothInterpolation = true;

    [Header("Stabilisation")]
    [Tooltip("Vitesse de rotation du truck vers sa direction (plus grand = plus reactif)")]
    public float rotationSmoothSpeed = 5f;

    private List<IMUFrame> frames = new List<IMUFrame>();
    private float elapsedTime = 0f;
    private bool isPlaying = false;
    private int currentFrameIndex = 0;
    private bool dataLoaded = false;

    private Vector3 startPositionInUnity;
    private Vector3 gpsOrigin;

    [System.Serializable]
    public class IMUFrame
    {
        public float time;
        public Vector3 position;
    }

    void Start()
    {
        startPositionInUnity = vehicleObject.transform.position;

        LoadCSV();

        if (dataLoaded && vehicleObject != null)
        {
            gpsOrigin = frames[0].position * positionScale;
            isPlaying = true;
            Debug.Log($"[GPSIMUPlayer] {frames.Count} frames chargees. Depart: {startPositionInUnity}");
        }
        else
        {
            if (!dataLoaded)           Debug.LogError("[GPSIMUPlayer] CSV introuvable !");
            if (vehicleObject == null) Debug.LogError("[GPSIMUPlayer] Vehicle Object non assigne !");
        }
    }

    void Update()
    {
        if (!isPlaying || !dataLoaded || vehicleObject == null) return;

        elapsedTime += Time.deltaTime * playbackSpeed;
        float totalDuration = frames[frames.Count - 1].time;

        if (elapsedTime >= totalDuration)
        {
            if (loop) { elapsedTime = 0f; currentFrameIndex = 0; }
            else      { elapsedTime = totalDuration; isPlaying = false; return; }
        }

        while (currentFrameIndex < frames.Count - 2 && frames[currentFrameIndex + 1].time <= elapsedTime)
            currentFrameIndex++;

        IMUFrame frameA = frames[currentFrameIndex];
        IMUFrame frameB = frames[Mathf.Min(currentFrameIndex + 1, frames.Count - 1)];

        // ── Position interpolée ───────────────────────────────────────────────
        Vector3 rawPos;
        if (smoothInterpolation && frameB.time > frameA.time)
        {
            float t = Mathf.Clamp01((elapsedTime - frameA.time) / (frameB.time - frameA.time));
            rawPos = Vector3.Lerp(frameA.position, frameB.position, t) * positionScale;
        }
        else
        {
            rawPos = frameA.position * positionScale;
        }

        Vector3 newPos = startPositionInUnity + (rawPos - gpsOrigin);

        // ── Rotation : direction vers la frame suivante ───────────────────────
        Vector3 futurePos = startPositionInUnity + (frameB.position * positionScale - gpsOrigin);
        Vector3 moveDir = futurePos - newPos;
        moveDir.y = 0f;

        if (moveDir.magnitude > 0.001f)
        {
            // Vector3.up force l'engin à rester parfaitement horizontal — pas de roll
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);

            vehicleObject.transform.rotation = Quaternion.Slerp(
                vehicleObject.transform.rotation,
                targetRot,
                Time.deltaTime * rotationSmoothSpeed
            );

            // Sécurité : bloquer pitch et roll, garder uniquement Yaw (rotation Y)
            Vector3 euler = vehicleObject.transform.rotation.eulerAngles;
            vehicleObject.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        // ── Position véhicule ─────────────────────────────────────────────────
        vehicleObject.transform.position = newPos;

        // ── Camera gérée par Cinemachine via Player Camera Root (enfant) ──────
        // Aucun code nécessaire ici
    }

    void LoadCSV()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[GPSIMUPlayer] Fichier introuvable : {path}");
            return;
        }

        frames.Clear();
        string[] lines = File.ReadAllLines(path);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] cols = line.Split(',');
            if (cols.Length < 4) continue;

            try
            {
                IMUFrame frame = new IMUFrame
                {
                    time     = ParseFloat(cols[0]),
                    position = new Vector3(ParseFloat(cols[1]), ParseFloat(cols[3]), ParseFloat(cols[2]))
                };
                frames.Add(frame);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GPSIMUPlayer] Ligne {i} ignoree : {e.Message}");
            }
        }

        dataLoaded = frames.Count > 0;
        if (dataLoaded) Debug.Log($"[GPSIMUPlayer] {frames.Count} frames chargees.");
    }

    float ParseFloat(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);

    public void Play()    => isPlaying = true;
    public void Pause()   => isPlaying = false;
    public void Restart() { elapsedTime = 0f; currentFrameIndex = 0; isPlaying = true; }

    Vector3 GPSToUnity(Vector3 gpsPos)
    {
        if (frames == null || frames.Count == 0) return gpsPos * positionScale;
        Vector3 origin = frames[0].position * positionScale;
        Vector3 start  = (vehicleObject != null) ? vehicleObject.transform.position : Vector3.zero;
        Vector3 anchor = Application.isPlaying ? startPositionInUnity : start;
        return anchor + (gpsPos * positionScale - origin);
    }

    void OnDrawGizmos()
    {
        if (!drawPath || frames == null || frames.Count < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < frames.Count - 1; i++)
            Gizmos.DrawLine(GPSToUnity(frames[i].position), GPSToUnity(frames[i + 1].position));
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GPSToUnity(frames[0].position), 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GPSToUnity(frames[frames.Count - 1].position), 0.5f);
    }

    void OnGUI()
    {
        if (!dataLoaded) return;
        float total    = frames[frames.Count - 1].time;
        float progress = elapsedTime / total;

        GUI.Box(new Rect(10, 10, 260, 95), "GPS/IMU Player");
        GUI.Label(new Rect(20, 35, 240, 20), $"Temps : {elapsedTime:F1}s / {total:F0}s");
        GUI.Label(new Rect(20, 55, 240, 20), $"Frame : {currentFrameIndex + 1} / {frames.Count}");
        GUI.Box(new Rect(20, 78, 230, 12), "");
        GUI.Box(new Rect(20, 78, 230 * progress, 12), "");
    }
}
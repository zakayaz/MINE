using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Records the position and rotation of a GameObject every frame
/// and saves the data to a CSV file when the game stops.
///
/// CSV format:
/// Timestamp, PosX, PosY, PosZ, RotX, RotY, RotZ, RotW
/// </summary>
public class VehicleRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [Tooltip("Folder where the CSV will be saved. Leave blank to use Application.persistentDataPath.")]
    public string outputFolder = "";

    [Tooltip("Base name for the output file. A timestamp will be appended automatically.")]
    public string fileBaseName = "Truck_Log";

    [Tooltip("Record every N frames. 1 = every frame (max data), 2 = every other frame, etc.")]
    [Range(1, 10)]
    public int recordEveryNFrames = 1;

    [Tooltip("Maximum number of frames to store in memory (0 = unlimited).")]
    public int maxFrames = 0;

    // ── Internal State ──────────────────────────────────────────────────

    private struct FrameData
    {
        public double timestamp;
        public float  posX, posY, posZ;
        public float  rotX, rotY, rotZ, rotW;
    }

    private List<FrameData> _frames    = new List<FrameData>();
    private int             _frameCount = 0;
    private bool            _isRecording = false;
    private double          _startTime   = 0.0;

    // ── Unity Lifecycle ─────────────────────────────────────────────────

    void Start()
    {Debug.Log("[VehicleRecorder] Files will be saved to: " + Application.persistentDataPath);
    StartRecording();
        
    }

    void Update()
    {
        if (!_isRecording) return;

        // Skip frames if recordEveryNFrames > 1
        _frameCount++;
        if (_frameCount % recordEveryNFrames != 0) return;

        // Stop recording if max frames reached
        if (maxFrames > 0 && _frames.Count >= maxFrames)
        {
            Debug.LogWarning($"[VehicleRecorder] Max frame limit ({maxFrames}) reached. Recording stopped.");
            _isRecording = false;
            return;
        }

        // Capture current frame
        Vector3    pos = transform.position;
        Quaternion rot = transform.rotation;

        _frames.Add(new FrameData
        {
            timestamp = Time.timeAsDouble - _startTime,
            posX = pos.x, posY = pos.y, posZ = pos.z,
            rotX = rot.x, rotY = rot.y, rotZ = rot.z, rotW = rot.w
        });
    }

    /// <summary>
    /// Called automatically when the game stops (editor or build).
    /// Saves all recorded frames to a CSV file.
    /// </summary>
    void OnApplicationQuit()
    {
        StopAndSave();
    }

    /// <summary>
    /// Also save if the object is destroyed mid-session (scene change, etc.)
    /// </summary>
    void OnDestroy()
    {
        if (_isRecording) StopAndSave();
    }

    // ── Public Controls ─────────────────────────────────────────────────

    /// <summary>Begin recording from scratch.</summary>
    public void StartRecording()
    {
        _frames.Clear();
        _frameCount  = 0;
        _startTime   = Time.timeAsDouble;
        _isRecording = true;
        Debug.Log("[VehicleRecorder] Recording started.");
    }

    /// <summary>Pause recording without discarding data.</summary>
    public void PauseRecording()  => _isRecording = false;

    /// <summary>Resume a paused recording.</summary>
    public void ResumeRecording() => _isRecording = true;

    /// <summary>Stop recording and save to CSV immediately.</summary>
    public void StopAndSave()
    {
        _isRecording = false;

        if (_frames.Count == 0)
        {
            Debug.LogWarning("[VehicleRecorder] No data to save.");
            return;
        }

        string path = BuildFilePath();
        SaveCSV(path);
    }

    // ── File Handling ───────────────────────────────────────────────────

    private string BuildFilePath()
    {
        // Choose output folder
        string folder = string.IsNullOrEmpty(outputFolder)
            ? Application.persistentDataPath
            : outputFolder;

        // Create folder if it doesn't exist
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
            Debug.Log($"[VehicleRecorder] Created output folder: {folder}");
        }

        // Append a date-time stamp so files never overwrite each other
        string datestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName  = $"{fileBaseName}_{datestamp}.csv";

        return Path.Combine(folder, fileName);
    }

    private void SaveCSV(string path)
    {
        try
        {
            // Pre-allocate StringBuilder for performance on large datasets
            var sb = new StringBuilder(_frames.Count * 80);

            // Header
            sb.AppendLine("Timestamp,PosX,PosY,PosZ,RotX,RotY,RotZ,RotW");

            // Data rows
            foreach (var f in _frames)
            {
                sb.Append(f.timestamp.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.posX.ToString("F4",      CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.posY.ToString("F4",      CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.posZ.ToString("F4",      CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.rotX.ToString("F6",      CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.rotY.ToString("F6",      CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.rotZ.ToString("F6",      CultureInfo.InvariantCulture)).Append(',');
                sb.AppendLine(f.rotW.ToString("F6",  CultureInfo.InvariantCulture));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            Debug.Log($"[VehicleRecorder] Saved {_frames.Count} frames → {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VehicleRecorder] Failed to save CSV: {ex.Message}");
        }
    }

    // ── Debug Info ──────────────────────────────────────────────────────

    /// <summary>Returns how many frames have been recorded so far.</summary>
    public int GetFrameCount() => _frames.Count;

    /// <summary>Returns total recorded duration in seconds.</summary>
    public float GetDuration() =>
        _frames.Count > 0 ? (float)_frames[_frames.Count - 1].timestamp : 0f;

    void OnGUI()
    {
        if (!_isRecording) return;

        // Small live counter in the top-left corner (disable in production)
        GUI.Label(new Rect(10, 10, 300, 25),
            $"● REC  {_frames.Count} frames  |  {GetDuration():F1}s");
    }
}

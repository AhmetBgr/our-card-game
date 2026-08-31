using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="SoundEffect"/>: the authored fields laid out so the ones that do nothing are
/// hidden, plus preview transport.
///
/// Built on SerializedProperty rather than writing to the target directly, which the original did. That
/// one call to DrawDefaultInspector followed by assignments to sound.volume meant no undo, no multi-object
/// editing, no prefab-override tracking, and an EditorUtility.SetDirty on every repaint -- so merely
/// LOOKING at a sound asset marked it changed and it got committed on the next save.
/// </summary>
[CustomEditor(typeof(SoundEffect))]
[CanEditMultipleObjects]
public class SoundEffectEditor : Editor
{
    private SerializedProperty _clips;
    private SerializedProperty _bus;
    private SerializedProperty _playOrder;
    private SerializedProperty _volume;
    private SerializedProperty _useRandomVolume;
    private SerializedProperty _volumeRandom;
    private SerializedProperty _pitch;
    private SerializedProperty _useRandomPitch;
    private SerializedProperty _pitchRandom;
    private SerializedProperty _loop;
    private SerializedProperty _spatialBlend;
    private SerializedProperty _priority;
    private SerializedProperty _minRetriggerInterval;
    private SerializedProperty _maxConcurrent;
    private SerializedProperty _stackVolumeFalloff;

    private void OnEnable()
    {
        _clips = serializedObject.FindProperty(nameof(SoundEffect.clips));
        _bus = serializedObject.FindProperty(nameof(SoundEffect.bus));
        _playOrder = serializedObject.FindProperty(nameof(SoundEffect.playOrder));
        _volume = serializedObject.FindProperty(nameof(SoundEffect.volume));
        _useRandomVolume = serializedObject.FindProperty(nameof(SoundEffect.useRandomVolume));
        _volumeRandom = serializedObject.FindProperty(nameof(SoundEffect.volumeRandom));
        _pitch = serializedObject.FindProperty(nameof(SoundEffect.pitch));
        _useRandomPitch = serializedObject.FindProperty(nameof(SoundEffect.useRandomPitch));
        _pitchRandom = serializedObject.FindProperty(nameof(SoundEffect.pitchRandom));
        _loop = serializedObject.FindProperty(nameof(SoundEffect.loop));
        _spatialBlend = serializedObject.FindProperty(nameof(SoundEffect.spatialBlend));
        _priority = serializedObject.FindProperty(nameof(SoundEffect.priority));
        _minRetriggerInterval = serializedObject.FindProperty(nameof(SoundEffect.minRetriggerInterval));
        _maxConcurrent = serializedObject.FindProperty(nameof(SoundEffect.maxConcurrent));
        _stackVolumeFalloff = serializedObject.FindProperty(nameof(SoundEffect.stackVolumeFalloff));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_clips, true);

        // Only meaningful with something to choose between, and showing it on a one-clip sound invites
        // the reader to think the setting is doing something.
        if (_clips.arraySize > 1) EditorGUILayout.PropertyField(_playOrder);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_bus);
        EditorGUILayout.PropertyField(_loop);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_useRandomVolume, new GUIContent("Randomize Volume"));
        if (_useRandomVolume.boolValue) DrawRangeField(_volumeRandom, "Volume Range", 0f, 1f);
        else EditorGUILayout.PropertyField(_volume);

        EditorGUILayout.PropertyField(_useRandomPitch, new GUIContent("Randomize Pitch"));
        if (_useRandomPitch.boolValue) DrawRangeField(_pitchRandom, "Pitch Range", -3f, 3f);
        else EditorGUILayout.PropertyField(_pitch);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_spatialBlend, new GUIContent("Spatial Blend (2D - 3D)"));
        EditorGUILayout.PropertyField(_priority);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Throttling", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_minRetriggerInterval, new GUIContent("Min Retrigger Interval"));
        EditorGUILayout.PropertyField(_maxConcurrent, new GUIContent("Max Concurrent"));

        using (new EditorGUI.DisabledScope(_maxConcurrent.intValue == 1))
            EditorGUILayout.PropertyField(_stackVolumeFalloff, new GUIContent("Stack Volume Falloff"));

        serializedObject.ApplyModifiedProperties();

        DrawWarnings();
        DrawPreviewControls();
    }

    /// <summary>
    /// A Vector2 drawn as the min/max pair it actually is, with the min held at or below the max so a
    /// reversed range (which Random.Range silently accepts and then always returns the low end of)
    /// cannot be authored.
    /// </summary>
    private static void DrawRangeField(SerializedProperty property, string label, float min, float max)
    {
        EditorGUI.indentLevel++;

        Vector2 range = property.vector2Value;

        EditorGUI.BeginChangeCheck();
        float newMin = EditorGUILayout.Slider("Min", range.x, min, max);
        float newMax = EditorGUILayout.Slider("Max", range.y, min, max);

        if (EditorGUI.EndChangeCheck())
        {
            // Whichever end the user just moved is the one that wins; push the other out of its way.
            if (!Mathf.Approximately(newMin, range.x)) newMax = Mathf.Max(newMin, newMax);
            else newMin = Mathf.Min(newMin, newMax);

            property.vector2Value = new Vector2(newMin, newMax);
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField(" ", $"{label}: {property.vector2Value.x:0.00} - {property.vector2Value.y:0.00}", EditorStyles.miniLabel);
    }

    private void DrawWarnings()
    {
        var sound = (SoundEffect)target;

        if (!sound.HasClips)
        {
            EditorGUILayout.HelpBox("No clips assigned. This sound will play silently.", MessageType.Warning);
            return;
        }

        bool hasEmptySlot = false;
        foreach (AudioClip clip in sound.clips)
        {
            if (clip == null) hasEmptySlot = true;
        }

        if (hasEmptySlot)
            EditorGUILayout.HelpBox("Some clip slots are empty. They fall back to the first assigned clip, which skews the variation.", MessageType.Warning);

        if (sound.loop && sound.maxConcurrent != 1)
            EditorGUILayout.HelpBox("A looping sound should usually set Max Concurrent to 1, or repeated plays will stack loops that only Stop() can clear.", MessageType.Info);
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.Space();

        var sound = (SoundEffect)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!sound.HasClips))
            {
                if (GUILayout.Button("Play")) sound.PlayPreview();
                if (GUILayout.Button("Play Reversed")) sound.PlayPreview(true);
            }

            using (new EditorGUI.DisabledScope(!sound.IsPreviewing))
            {
                if (GUILayout.Button("Stop")) sound.StopPreview();
            }
        }

        // The preview is an AudioSource ticking outside the inspector's own repaint, so the Stop button
        // would stay enabled after a clip ended without this.
        if (sound.IsPreviewing) Repaint();
    }
}

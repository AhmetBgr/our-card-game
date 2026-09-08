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
    private SerializedProperty _delay;
    private SerializedProperty _fadeOut;
    private SerializedProperty _fadeOutDuration;
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
        _delay = serializedObject.FindProperty(nameof(SoundEffect.delay));
        _fadeOut = serializedObject.FindProperty(nameof(SoundEffect.fadeOut));
        _fadeOutDuration = serializedObject.FindProperty(nameof(SoundEffect.fadeOutDuration));
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
        EditorGUILayout.PropertyField(_delay, new GUIContent("Delay (sec)"));

        EditorGUILayout.PropertyField(_fadeOut, new GUIContent("Fade Out When Stopped"));
        if (_fadeOut.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_fadeOutDuration, new GUIContent("Fade Duration"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_useRandomVolume, new GUIContent("Randomize Volume"));
        if (_useRandomVolume.boolValue) DrawVolumeRange(_volumeRandom);
        else EditorGUILayout.PropertyField(_volume);

        EditorGUILayout.PropertyField(_useRandomPitch, new GUIContent("Randomize Pitch"));
        if (_useRandomPitch.boolValue) DrawPitchRange(_pitchRandom);
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

    // ---------------------------------------------------------------------------------------------
    // Randomisation ranges.
    //
    // Both are a Vector2 min/max pair, drawn as one MinMaxSlider with a typed field on each end rather
    // than the two independent sliders this used to have. Two sliders let a designer author max < min,
    // which Random.Range accepts silently and then always returns the low end of; a MinMaxSlider cannot
    // express that at all, and the typed fields clamp to each other.
    //
    // Pitch additionally does not get a linear track. The range is -3..3 and the useful window is about
    // 0.95..1.05, so on a linear slider the entire authoring range is one percent of the track and
    // undialable by drag. The slider below runs in SEMITONES instead: 1.0 sits dead centre, a halving
    // and a doubling are the same distance from it, and a quarter-semitone nudge is a real drag rather
    // than a pixel. The stored value stays the raw multiplier the AudioSource wants.
    //
    // Negative pitch stays authorable, because a range like -1.05..-0.95 is a real sound: the clip
    // played backwards with the same jitter every other sound gets. Semitones cannot express a negative
    // ratio, so the slider works on the MAGNITUDE and the sign is carried through -- a wholly negative
    // range drags exactly like a positive one, and the Reverse button flips between the two. Only a
    // range that straddles zero has no semitone reading, and that one is typed rather than dragged.
    // ---------------------------------------------------------------------------------------------

    private const float FieldWidth = 52f;
    private const float Gap = 4f;

    /// <summary>Half-widths, in semitones, offered as one-click spreads under the pitch range.</summary>
    private static readonly float[] PitchSpreads = { 0.15f, 0.3f, 0.6f, 1.2f };

    private static readonly GUIContent[] PitchSpreadLabels =
    {
        new GUIContent("Tiny", "±0.15 semitones (about ±1%). Just enough to take the edge off a clip that fires many times a second."),
        new GUIContent("Subtle", "±0.3 semitones (about ±2%). The safe default for a repeated impact."),
        new GUIContent("Natural", "±0.6 semitones (about ±3.5%). Reads as separate takes without drawing attention."),
        new GUIContent("Wide", "±1.2 semitones (about ±7%). Audibly different every time -- for a sound that is allowed to be playful.")
    };

    /// <summary>
    /// Where the semitone slider ends: at the same 3x the pitch field itself stops at, so the track
    /// covers every magnitude that can be authored rather than pinning short of it.
    /// </summary>
    private static readonly float PitchSliderSemitones = 12f * Mathf.Log(PitchLimitMax, 2f);

    private static void DrawVolumeRange(SerializedProperty property)
    {
        EditorGUI.indentLevel++;

        Vector2 range = DrawRangeRow(
            property,
            new GUIContent("Volume Range", "Min and max volume. Keep the spread small; large jumps read as a bug rather than as variation."),
            0f, 1f, semitoneTrack: false);

        if (range != property.vector2Value) property.vector2Value = range;

        // Decibels, because that is the difference the ear actually hears: 0.5 to 1.0 looks like half
        // the volume and sounds like a quiet version of the same thing, not a different one.
        float spreadDb = range.x > 0.0001f ? 20f * Mathf.Log10(range.y / range.x) : float.PositiveInfinity;
        string spread = float.IsInfinity(spreadDb) ? "silent at the low end" : $"{spreadDb:0.0} dB spread";
        DrawReadout($"{range.x:0.00} – {range.y:0.00}   ·   {spread}");

        EditorGUI.indentLevel--;
    }

    private static void DrawPitchRange(SerializedProperty property)
    {
        EditorGUI.indentLevel++;

        Vector2 range = DrawRangeRow(
            property,
            new GUIContent("Pitch Range", "Min and max playback speed. The slider is spaced in semitones, so the small values that actually get used are dialable; type an exact multiplier in the fields. Negative values play the clip backwards."),
            PitchLimitMin, PitchLimitMax, semitoneTrack: true);

        if (range != property.vector2Value) property.vector2Value = range;

        DrawPitchSpreadPresets(property);

        string readout = $"{range.x:0.000}× – {range.y:0.000}×";

        if (TryGetMagnitudes(range, out Vector2 magnitudes, out float sign))
        {
            float halfSpread = (ToSemitones(magnitudes.y) - ToSemitones(magnitudes.x)) * 0.5f;
            readout += $"   ·   ±{halfSpread:0.00} semitones   ·   {DescribeSpread(halfSpread)}";
            if (sign < 0f) readout += "   ·   reversed";
        }
        else
        {
            readout += "   ·   straddles zero";
        }

        DrawReadout(readout);

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Bounds of the pitch range: the same -3..3 the <see cref="SoundEffect.pitch"/> field carries.
    /// Negative is a legitimate authoring choice -- it is the clip played backwards -- so the range is
    /// not clamped positive. What it cannot do is straddle zero; see <see cref="TryGetMagnitudes"/>.
    /// </summary>
    private const float PitchLimitMin = -3f;
    private const float PitchLimitMax = 3f;

    /// <summary>
    /// Splits a pitch range into the magnitude pair the semitone track works on and the direction it
    /// plays in. False for a range that straddles zero: forwards and backwards in one roll has no
    /// semitone reading, and rolls landing near 0 are silence that ResolvePitch has to replace with 1.
    /// </summary>
    private static bool TryGetMagnitudes(Vector2 range, out Vector2 magnitudes, out float sign)
    {
        if (range.x > 0f && range.y > 0f)
        {
            magnitudes = range;
            sign = 1f;
            return true;
        }

        if (range.x < 0f && range.y < 0f)
        {
            // Negating swaps the ends: -1.05 is the lower bound but the LARGER magnitude.
            magnitudes = new Vector2(-range.y, -range.x);
            sign = -1f;
            return true;
        }

        magnitudes = Vector2.one;
        sign = range.y < 0f ? -1f : 1f;
        return false;
    }

    /// <summary>The inverse of <see cref="TryGetMagnitudes"/>: magnitudes and a direction back to a range.</summary>
    private static Vector2 FromMagnitudes(Vector2 magnitudes, float sign) =>
        sign < 0f ? new Vector2(-magnitudes.y, -magnitudes.x) : magnitudes;

    /// <summary>
    /// One row: prefix label, an exact field on each end, and a MinMaxSlider between them. Returns the
    /// edited range without writing it, so the caller decides when the property is dirtied.
    /// </summary>
    private static Vector2 DrawRangeRow(SerializedProperty property, GUIContent label, float limitMin, float limitMax, bool semitoneTrack)
    {
        Rect row = EditorGUILayout.GetControlRect();
        Rect content = EditorGUI.PrefixLabel(row, label);

        // The prefix label has already consumed the indent; leaving it on would push the fields inside
        // the rects computed below.
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var minRect = new Rect(content.x, content.y, FieldWidth, content.height);
        var maxRect = new Rect(content.xMax - FieldWidth, content.y, FieldWidth, content.height);
        var sliderRect = new Rect(minRect.xMax + Gap, content.y, maxRect.x - minRect.xMax - Gap * 2f, content.height);

        Vector2 range = property.vector2Value;
        float min = range.x;
        float max = range.y;

        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

        // Delayed, so typing "0.9" does not clamp against the max the instant "0." is parsed as zero.
        EditorGUI.BeginChangeCheck();
        float typedMin = EditorGUI.DelayedFloatField(minRect, min);
        if (EditorGUI.EndChangeCheck())
        {
            min = Mathf.Clamp(typedMin, limitMin, limitMax);
            max = Mathf.Max(min, max);
        }

        EditorGUI.BeginChangeCheck();
        float typedMax = EditorGUI.DelayedFloatField(maxRect, max);
        if (EditorGUI.EndChangeCheck())
        {
            max = Mathf.Clamp(typedMax, limitMin, limitMax);
            min = Mathf.Min(min, max);
        }

        if (!semitoneTrack)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, limitMin, limitMax);
            if (EditorGUI.EndChangeCheck())
            {
                min = Round(min, false);
                max = Round(max, false);
            }
        }
        else if (TryGetMagnitudes(new Vector2(min, max), out Vector2 magnitudes, out float sign))
        {
            // Dragged in semitones over the magnitude, so a backwards range handles exactly like a
            // forwards one and only the sign on the way out says which it was.
            float trackMin = ToSemitones(magnitudes.x);
            float trackMax = ToSemitones(magnitudes.y);

            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderRect, ref trackMin, ref trackMax, -PitchSliderSemitones, PitchSliderSemitones);

            if (EditorGUI.EndChangeCheck())
            {
                // A drag through semitone space lands on values like 0.94387422; round to something a
                // designer can read back and re-type.
                var dragged = new Vector2(
                    Round(FromSemitones(trackMin), true),
                    Round(FromSemitones(trackMax), true));

                Vector2 signed = FromMagnitudes(dragged, sign);
                min = signed.x;
                max = signed.y;
            }
        }
        else
        {
            // No semitone reading to drag along. The fields still edit it, and the readout and the
            // warning below both say why the track is empty.
            EditorGUI.LabelField(sliderRect, "forwards and backwards — type the ends", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUI.showMixedValue = false;
        EditorGUI.indentLevel = indent;

        return new Vector2(Mathf.Min(min, max), Mathf.Max(min, max));
    }

    /// <summary>
    /// One-click spreads, applied around wherever the range is currently centred rather than around 1.0 --
    /// a sound deliberately pitched down to 0.8 keeps its centre and only changes how far it wanders.
    /// </summary>
    private static void DrawPitchSpreadPresets(SerializedProperty property)
    {
        Rect row = EditorGUILayout.GetControlRect();
        Rect content = EditorGUI.PrefixLabel(row, new GUIContent("Spread", "Set how far the pitch wanders, keeping the current centre."));

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        Vector2 range = property.vector2Value;
        bool signed = TryGetMagnitudes(range, out Vector2 magnitudes, out float sign);

        float centre = GeometricCentre(magnitudes);
        float currentHalf = signed ? (ToSemitones(magnitudes.y) - ToSemitones(magnitudes.x)) * 0.5f : -1f;

        // The last cell of the row is the direction toggle rather than a spread.
        float width = content.width / (PitchSpreads.Length + 1);

        for (int i = 0; i < PitchSpreads.Length; i++)
        {
            var buttonRect = new Rect(content.x + width * i, content.y, width, content.height);
            GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonMid;

            // Toggle rather than Button so the spread currently in use reads as selected.
            bool active = signed && Mathf.Abs(currentHalf - PitchSpreads[i]) < 0.02f;

            if (GUI.Toggle(buttonRect, active, PitchSpreadLabels[i], style) && !active)
            {
                float half = PitchSpreads[i];
                var spread = new Vector2(
                    Round(Mathf.Clamp(centre * SemitoneRatio(-half), 0.01f, PitchLimitMax), true),
                    Round(Mathf.Clamp(centre * SemitoneRatio(half), 0.01f, PitchLimitMax), true));

                // A spread preset changes how far the pitch wanders, never which way the clip runs, so
                // the sign the range already had is put back on. A straddling range is taken forwards.
                property.vector2Value = FromMagnitudes(spread, signed ? sign : 1f);

                // Otherwise a focused min/max field keeps showing the value it had before the preset.
                GUI.FocusControl(null);
            }
        }

        var reverseRect = new Rect(content.xMax - width, content.y, width, content.height);
        bool reversed = signed && sign < 0f;

        var reverseLabel = new GUIContent(
            "Reverse",
            "Flip the whole range negative, which plays the clip backwards with the same jitter. The same thing ApplyTo's playReverse flag does, but authored into the asset.");

        if (GUI.Toggle(reverseRect, reversed, reverseLabel, EditorStyles.miniButtonRight) != reversed)
        {
            // Straddling ranges have no single direction to flip; leave those to the fields.
            if (signed)
            {
                property.vector2Value = FromMagnitudes(magnitudes, reversed ? 1f : -1f);
                GUI.FocusControl(null);
            }
        }

        EditorGUI.indentLevel = indent;
    }

    private static void DrawReadout(string text)
    {
        Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight - 2f);
        EditorGUI.LabelField(EditorGUI.IndentedRect(row), text, EditorStyles.miniLabel);
    }

    private static string DescribeSpread(float halfSemitones)
    {
        if (halfSemitones < 0.05f) return "no variation";
        if (halfSemitones < 0.2f) return "barely audible";
        if (halfSemitones < 0.45f) return "subtle";
        if (halfSemitones < 0.9f) return "natural";
        if (halfSemitones < 2f) return "wide";
        return "extreme -- reads as a different sound";
    }

    private static float ToSemitones(float ratio) =>
        ratio <= 0.0001f ? -PitchSliderSemitones : 12f * Mathf.Log(ratio, 2f);

    private static float FromSemitones(float semitones) => Mathf.Pow(2f, semitones / 12f);

    private static float SemitoneRatio(float semitones) => Mathf.Pow(2f, semitones / 12f);

    /// <summary>
    /// The centre of a pitch range is its geometric mean, not its average: a semitone up and a semitone
    /// down from 1.0 are 1.059 and 0.944, which average to 1.002 rather than back to 1. Takes magnitudes,
    /// so a backwards range centres on the speed it runs at rather than collapsing to 1.
    /// </summary>
    private static float GeometricCentre(Vector2 magnitudes) =>
        magnitudes.x <= 0.0001f || magnitudes.y <= 0.0001f ? 1f : Mathf.Sqrt(magnitudes.x * magnitudes.y);

    private static float Round(float value, bool fine) =>
        fine ? Mathf.Round(value * 1000f) / 1000f : Mathf.Round(value * 100f) / 100f;

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

        // A wholly negative range is fine -- that is the clip backwards. One that straddles zero is not:
        // the direction changes per roll, and anything landing on 0 is silence ResolvePitch replaces
        // with 1, so the sound occasionally plays at an unrelated speed.
        if (sound.useRandomPitch && sound.pitchRandom.x <= 0f && sound.pitchRandom.y >= 0f)
            EditorGUILayout.HelpBox("The pitch range crosses 0, so each play picks at random between forwards and backwards -- and a roll near 0 is silence that is replaced by pitch 1. Keep both ends on the same side of 0.", MessageType.Warning);

        if (sound.loop && sound.maxConcurrent != 1)
            EditorGUILayout.HelpBox("A looping sound should usually set Max Concurrent to 1, or repeated plays will stack loops that only Stop() can clear.", MessageType.Info);

        // A loop is stopped by code at an arbitrary point in the waveform, so it is the case where
        // cutting is most likely to be heard as a click.
        if (sound.loop && !sound.fadeOut)
            EditorGUILayout.HelpBox("A looping sound is stopped mid-waveform, which usually clicks. Consider turning Fade Out When Stopped on.", MessageType.Info);

        // Long enough to read as lag rather than as timing, and it is easy to leave a stray value here.
        if (sound.delay > 0.5f)
            EditorGUILayout.HelpBox($"This sound waits {sound.delay:0.00}s before it is heard, on top of any delay the call site passes.", MessageType.Info);
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

        DrawSequenceControls(sound);

        // The preview is an AudioSource ticking outside the inspector's own repaint, so the Stop button
        // would stay enabled after a clip ended without this.
        if (sound.IsPreviewing || AttackSequencePreview.IsPlaying) Repaint();
    }

    /// <summary>
    /// The whole attack, for the sounds that are part of one: select, strike and hit played back to back
    /// with the game's own spacing. Tuning any one of the three against the other two is the only way to
    /// hear what the player will -- alone, a hit sound reveals nothing about whether the strike swallows it.
    ///
    /// Only drawn on the six assets the sequences are built from, so it does not become noise on the
    /// forty-odd other sounds.
    /// </summary>
    private void DrawSequenceControls(SoundEffect sound)
    {
        bool melee = AttackSequencePreview.IsPartOfMelee(sound);
        bool ranged = AttackSequencePreview.IsPartOfRanged(sound);

        if (!melee && !ranged) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Full Attack (select → strike → hit)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Play Melee"))
            {
                // The single-sound previewer is a separate AudioSource that would otherwise keep playing
                // underneath the sequence.
                sound.StopPreview();
                AttackSequencePreview.PlayMelee();
            }

            if (GUILayout.Button("Play Ranged"))
            {
                sound.StopPreview();
                AttackSequencePreview.PlayRanged();
            }

            using (new EditorGUI.DisabledScope(!AttackSequencePreview.IsPlaying))
            {
                if (GUILayout.Button("Stop")) AttackSequencePreview.Stop();
            }
        }

        // Reported for the sequence being tuned, not for both, so a missing ranged clip does not nag on a
        // melee asset.
        string missing = AttackSequencePreview.MissingSteps(ranged && !melee);
        if (missing != null) EditorGUILayout.HelpBox(missing, MessageType.Info);
    }
}

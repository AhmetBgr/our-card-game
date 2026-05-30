using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class CardBrowserWindow : EditorWindow
{
    private enum SortMode { Name, Cost, Attack, Health, Type }

    [Serializable]
    private class FilterState
    {
        public string search = "";

        public List<int> selectedCosts = new List<int>();

        public bool upgradedOnly = false;
        public bool baseOnly = false;

        public bool minionsOnly = false;
        public bool spellsOnly = false;

        public bool meleeOnly = false;
        public bool rangedOnly = false;

        public bool noDescriptionsOnly = false;

        public bool showTypeNone = true;
        public bool showTypeBuff = true;
        public bool showTypeDebuff = true;

        public SortMode sortMode = SortMode.Name;
        public bool sortAscending = true;
    }

    private const string PrefsKey = "CardBrowserWindow.FilterState.v1";

    private FilterState _filters = new FilterState();

    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    private SearchField _searchField;

    private List<CardSO> _allCards = new List<CardSO>();
    private List<CardSO> _visibleCards = new List<CardSO>();

    private CardSO _selected;
    private Editor _selectedEditor;

    private GUIStyle _listItemStyle;

    // Pixel distance the mouse must move from the press point before a list-item
    // drag begins. Anything below this is treated as a click (selection).
    private const float DragStartThreshold = 8f;
    private Vector2 _mouseDownPos;

    [MenuItem("Tools/Cards/Card Browser")]
    public static void Open()
    {
        CardBrowserWindow window = GetWindow<CardBrowserWindow>("Card Browser");
        window.minSize = new Vector2(900, 500);
        window.Show();
    }

    private void OnEnable()
    {
        _searchField = new SearchField();
        LoadPrefs();
        RefreshCards();
    }

    private void OnDisable()
    {
        SavePrefs();
        DestroySelectedEditor();
    }

    private void OnFocus()
    {
        // In case assets changed while window was unfocused.
        RefreshCardsIfNeeded();
    }

    private void RefreshCardsIfNeeded()
    {
        if (_allCards.Count == 0)
            RefreshCards();
    }

    private void RefreshCards()
    {
        string[] guids = AssetDatabase.FindAssets("t:CardSO");
        _allCards = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => { var p = path.ToLowerInvariant(); return p.Contains("/cards/") && !p.Contains("/test/"); })
            .Select(path => AssetDatabase.LoadAssetAtPath<CardSO>(path))
            .Where(c => c != null)
            .ToList();

        ApplyFilters();

        if (_selected == null && _visibleCards.Count > 0)
            Select(_visibleCards[0]);
        else if (_selected != null && !_allCards.Contains(_selected))
            Select(null);
    }

    private void ApplyFilters()
    {
        IEnumerable<CardSO> query = _allCards;

        if (!string.IsNullOrWhiteSpace(_filters.search))
        {
            string needle = _filters.search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                (!string.IsNullOrEmpty(c.cardName) && c.cardName.ToLowerInvariant().Contains(needle)) ||
                (!string.IsNullOrEmpty(c.name) && c.name.ToLowerInvariant().Contains(needle)) ||
                (!string.IsNullOrEmpty(c.desc) && c.desc.ToLowerInvariant().Contains(needle))
            );
        }

        if (_filters.selectedCosts.Count > 0)
            query = query.Where(c => _filters.selectedCosts.Contains(c.cost));

        if (_filters.upgradedOnly)
            query = query.Where(c => c.isUpgraded);

        if (_filters.baseOnly)
            query = query.Where(c => !c.isUpgraded);

        if (_filters.minionsOnly)
            query = query.Where(IsMinionLike);

        if (_filters.spellsOnly)
            query = query.Where(IsSpellLike);

        if (_filters.meleeOnly)
            query = query.Where(c => c.range == 1);

        if (_filters.rangedOnly)
            query = query.Where(c => c.range == 2);

        if (_filters.noDescriptionsOnly)
            query = query.Where(c => string.IsNullOrWhiteSpace(c.desc));

        query = query.Where(c => MatchesTypeFilter(c.type));

        query = _filters.sortMode switch
        {
            SortMode.Cost    => _filters.sortAscending ? query.OrderBy(c => c.cost).ThenBy(c => DisplayName(c))    : query.OrderByDescending(c => c.cost).ThenBy(c => DisplayName(c)),
            SortMode.Attack  => _filters.sortAscending ? query.OrderBy(c => c.attack).ThenBy(c => DisplayName(c))  : query.OrderByDescending(c => c.attack).ThenBy(c => DisplayName(c)),
            SortMode.Health  => _filters.sortAscending ? query.OrderBy(c => c.health).ThenBy(c => DisplayName(c))  : query.OrderByDescending(c => c.health).ThenBy(c => DisplayName(c)),
            SortMode.Type    => _filters.sortAscending ? query.OrderBy(c => c.type).ThenBy(c => DisplayName(c))    : query.OrderByDescending(c => c.type).ThenBy(c => DisplayName(c)),
            _                => _filters.sortAscending ? query.OrderBy(c => DisplayName(c))                         : query.OrderByDescending(c => DisplayName(c)),
        };

        _visibleCards = query.ToList();

        if (_selected != null && !_visibleCards.Contains(_selected))
            Select(null);
    }

    private bool MatchesTypeFilter(CardSO.Type type)
    {
        return type switch
        {
            CardSO.Type.None => _filters.showTypeNone,
            CardSO.Type.Buff => _filters.showTypeBuff,
            CardSO.Type.Debuff => _filters.showTypeDebuff,
            _ => true
        };
    }

    // Project doesn't have an explicit "Minion" type field, so use a heuristic:
    // - Has minion art, OR
    // - Has combat stats.
    private static bool IsMinionLike(CardSO card)
    {
        if (card == null)
            return false;

        if (card.minionArt != null)
            return true;

        return card.attack > 0 || card.health > 0;
    }

    private static bool IsSpellLike(CardSO card)
    {
        if (card == null)
            return false;

        return card.attack <= 0 && card.health <= 0;
    }

    private static string DisplayName(CardSO card)
    {
        if (card == null)
            return "<null>";
        return string.IsNullOrWhiteSpace(card.cardName) ? card.name : card.cardName;
    }

    private void OnGUI()
    {
        EnsureStyles();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftPane();
            DrawRightPane();
        }
    }

    private void EnsureStyles()
    {
        if (_listItemStyle != null)
            return;

        _listItemStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft
        };

    }

    private void DrawLeftPane()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(380)))
        {
            DrawToolbar();
            DrawFilters();

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Cards: {_visibleCards.Count}", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    if (_selected != null)
                    {
                        EditorGUIUtility.PingObject(_selected);
                        Selection.activeObject = _selected;
                    }
                }
            }

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            DrawCardList();
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RefreshCards();

            GUILayout.Space(8);

            GUILayout.Label("Search", GUILayout.Width(44));

            string nextSearch = (_searchField ??= new SearchField()).OnToolbarGUI(_filters.search, GUILayout.ExpandWidth(true));
            if (nextSearch != _filters.search)
            {
                _filters.search = nextSearch;
                ApplyFilters();
                SavePrefs();
            }
        }
    }

    private void DrawFilters()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {


            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Cost", GUILayout.Width(32));
                bool changed = false;
                for (int cost = 0; cost <= 10; cost++)
                {
                    bool isOn = _filters.selectedCosts.Contains(cost);
                    GUIStyle style = cost == 0 ? EditorStyles.miniButtonLeft
                                   : cost == 10 ? EditorStyles.miniButtonRight
                                   : EditorStyles.miniButtonMid;
                    bool next = GUILayout.Toggle(isOn, cost.ToString(), style, GUILayout.Width(26));
                    if (next != isOn)
                    {
                        if (next) _filters.selectedCosts.Add(cost);
                        else _filters.selectedCosts.Remove(cost);
                        changed = true;
                    }
                }
                if (changed) { ApplyFilters(); SavePrefs(); }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool nextUpgradedOnly = EditorGUILayout.ToggleLeft("Upgraded Only", _filters.upgradedOnly);
                bool nextBaseOnly = EditorGUILayout.ToggleLeft("Base Only", _filters.baseOnly);

                if (nextUpgradedOnly != _filters.upgradedOnly || nextBaseOnly != _filters.baseOnly)
                {
                    _filters.upgradedOnly = nextUpgradedOnly;
                    _filters.baseOnly = nextBaseOnly;

                    // Prevent accidental "no results" state if both are toggled.
                    if (_filters.upgradedOnly && _filters.baseOnly)
                        _filters.baseOnly = false;

                    ApplyFilters();
                    SavePrefs();
                }
            }

            bool nextMinionsOnly = EditorGUILayout.ToggleLeft("Minions Only (heuristic)", _filters.minionsOnly);
            if (nextMinionsOnly != _filters.minionsOnly)
            {
                _filters.minionsOnly = nextMinionsOnly;
                if (_filters.minionsOnly && _filters.spellsOnly)
                    _filters.spellsOnly = false;
                ApplyFilters();
                SavePrefs();
            }

            bool nextSpellsOnly = EditorGUILayout.ToggleLeft("Spells Only (no ATK/HP)", _filters.spellsOnly);
            if (nextSpellsOnly != _filters.spellsOnly)
            {
                _filters.spellsOnly = nextSpellsOnly;
                if (_filters.spellsOnly && _filters.minionsOnly)
                    _filters.minionsOnly = false;
                ApplyFilters();
                SavePrefs();
            }

            bool nextMeleeOnly = EditorGUILayout.ToggleLeft("Melee Only (range 1)", _filters.meleeOnly);
            if (nextMeleeOnly != _filters.meleeOnly)
            {
                _filters.meleeOnly = nextMeleeOnly;
                if (_filters.meleeOnly && _filters.rangedOnly)
                    _filters.rangedOnly = false;
                ApplyFilters();
                SavePrefs();
            }

            bool nextRangedOnly = EditorGUILayout.ToggleLeft("Ranged Only (range 2)", _filters.rangedOnly);
            if (nextRangedOnly != _filters.rangedOnly)
            {
                _filters.rangedOnly = nextRangedOnly;
                if (_filters.rangedOnly && _filters.meleeOnly)
                    _filters.meleeOnly = false;
                ApplyFilters();
                SavePrefs();
            }

            bool nextNoDescriptionsOnly = EditorGUILayout.ToggleLeft("No Descriptions", _filters.noDescriptionsOnly);
            if (nextNoDescriptionsOnly != _filters.noDescriptionsOnly)
            {
                _filters.noDescriptionsOnly = nextNoDescriptionsOnly;
                ApplyFilters();
                SavePrefs();
            }

            EditorGUILayout.Space(4);

            GUILayout.Label("Type", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool nextNone = GUILayout.Toggle(_filters.showTypeNone, "None", EditorStyles.miniButtonLeft);
                bool nextBuff = GUILayout.Toggle(_filters.showTypeBuff, "Buff", EditorStyles.miniButtonMid);
                bool nextDebuff = GUILayout.Toggle(_filters.showTypeDebuff, "Debuff", EditorStyles.miniButtonRight);

                if (nextNone != _filters.showTypeNone || nextBuff != _filters.showTypeBuff || nextDebuff != _filters.showTypeDebuff)
                {
                    _filters.showTypeNone = nextNone;
                    _filters.showTypeBuff = nextBuff;
                    _filters.showTypeDebuff = nextDebuff;
                    ApplyFilters();
                    SavePrefs();
                }
            }

            EditorGUILayout.Space(4);

            GUILayout.Label("Sort", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                SortMode[] modes = (SortMode[])System.Enum.GetValues(typeof(SortMode));
                for (int i = 0; i < modes.Length; i++)
                {
                    GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft
                                   : i == modes.Length - 1 ? EditorStyles.miniButtonRight
                                   : EditorStyles.miniButtonMid;
                    bool isActive = _filters.sortMode == modes[i];
                    bool next = GUILayout.Toggle(isActive, modes[i].ToString(), style);
                    if (next && !isActive)
                    {
                        _filters.sortMode = modes[i];
                        ApplyFilters();
                        SavePrefs();
                    }
                }

                GUILayout.Space(6);

                string dirLabel = _filters.sortAscending ? "▲ Asc" : "▼ Desc";
                if (GUILayout.Button(dirLabel, EditorStyles.miniButton, GUILayout.Width(54)))
                {
                    _filters.sortAscending = !_filters.sortAscending;
                    ApplyFilters();
                    SavePrefs();
                }
            }
        }
    }

    private void DrawCardList()
    {
        if (_visibleCards.Count == 0)
        {
            EditorGUILayout.HelpBox("No cards match the current filters.", MessageType.Info);
            return;
        }

        bool grouped = !_filters.upgradedOnly && !_filters.baseOnly;

        for (int i = 0; i < _visibleCards.Count; i++)
        {
            CardSO card = _visibleCards[i];
            if (card == null)
                continue;

            if (grouped && card.isUpgraded)
                continue;

            DrawCardRow(card, 0);

            if (grouped && card.upgradedVersion != null)
                DrawCardRow(card.upgradedVersion, 16);
        }

        if (grouped)
        {
            foreach (CardSO card in _visibleCards)
            {
                if (card == null || !card.isUpgraded)
                    continue;
                if (FindBaseCard(card) == null)
                    DrawCardRow(card, 0);
            }
        }
    }

    private void DrawCardRow(CardSO card, float indent)
    {
        bool isSelected = card == _selected;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (indent > 0)
                GUILayout.Space(indent);

            Texture icon = null;
            if (card.cardArt != null)
                icon = AssetPreview.GetAssetPreview(card.cardArt) ?? AssetPreview.GetMiniThumbnail(card.cardArt);
            if (icon == null)
                icon = AssetPreview.GetMiniThumbnail(card);

            GUIContent content = new GUIContent($"{DisplayName(card)}   (Cost {card.cost})", icon);

            Rect r = GUILayoutUtility.GetRect(content, _listItemStyle, GUILayout.Height(20));
            int id = GUIUtility.GetControlID(FocusType.Passive);

            if (isSelected)
                EditorGUI.DrawRect(r, new Color(0.2f, 0.4f, 0.85f, 0.25f));

            GUIStyle whiteText = new GUIStyle(GUIStyle.none);
            whiteText.normal.textColor = Color.white;
            whiteText.hover.textColor = Color.white;

            switch (Event.current.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (r.Contains(Event.current.mousePosition) && Event.current.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        _mouseDownPos = Event.current.mousePosition;
                        Event.current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        if (r.Contains(Event.current.mousePosition))
                            Select(card);
                        Event.current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id &&
                        (Event.current.mousePosition - _mouseDownPos).sqrMagnitude >= DragStartThreshold * DragStartThreshold)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new UnityEngine.Object[] { card };
                        DragAndDrop.StartDrag(card.name);
                        GUIUtility.hotControl = 0;
                        Event.current.Use();
                    }
                    break;
                case EventType.Repaint:
                    whiteText.Draw(r, content, r.Contains(Event.current.mousePosition), GUIUtility.hotControl == id, isSelected, false);
                    break;
            }

            // Right-click context menu.
            if (Event.current.type == EventType.ContextClick && r.Contains(Event.current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Select in Project"), false, () =>
                {
                    Selection.activeObject = card;
                    EditorGUIUtility.PingObject(card);
                });
                menu.AddItem(new GUIContent("Open Inspector"), false, () =>
                {
                    Selection.activeObject = card;
                    EditorGUIUtility.PingObject(card);
                });
                menu.ShowAsContext();
                Event.current.Use();
            }
        }
    }

    private CardSO FindBaseCard(CardSO upgraded)
    {
        return _allCards.FirstOrDefault(c => c.upgradedVersion == upgraded);
    }

    private void DrawRightPane()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(_selected != null ? $"Editing: {DisplayName(_selected)}" : "No card selected", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(52)) && _selected != null)
                    Selection.activeObject = _selected;

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44)) && _selected != null)
                {
                    EditorUtility.SetDirty(_selected);
                    AssetDatabase.SaveAssets();
                }
            }

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Pick a card from the list on the left to view/edit it.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (_selectedEditor == null || _selectedEditor.target != _selected)
            {
                DestroySelectedEditor();
                _selectedEditor = Editor.CreateEditor(_selected);
            }

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                _selectedEditor.OnInspectorGUI();
                if (change.changed)
                {
                    EditorUtility.SetDirty(_selected);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void Select(CardSO card)
    {
        if (_selected == card)
            return;

        _selected = card;
        DestroySelectedEditor();
        Repaint();
    }

    private void DestroySelectedEditor()
    {
        if (_selectedEditor == null)
            return;
        DestroyImmediate(_selectedEditor);
        _selectedEditor = null;
    }

    private void SavePrefs()
    {
        try
        {
            string json = JsonUtility.ToJson(_filters);
            EditorPrefs.SetString(PrefsKey, json);
        }
        catch
        {
            // Ignore prefs errors.
        }
    }

    private void LoadPrefs()
    {
        if (!EditorPrefs.HasKey(PrefsKey))
            return;

        try
        {
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrWhiteSpace(json))
                return;
            _filters = JsonUtility.FromJson<FilterState>(json) ?? new FilterState();
        }
        catch
        {
            _filters = new FilterState();
        }
    }
}

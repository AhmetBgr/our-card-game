using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class CardBrowserWindow : EditorWindow
{
    [Serializable]
    private class FilterState
    {
        public string search = "";

        public bool costEnabled = false;
        public int costMin = 0;
        public int costMax = 10;

        public bool upgradedOnly = false;
        public bool baseOnly = false;

        public bool minionsOnly = false;

        public bool noDescriptionsOnly = false;

        public bool showTypeNone = true;
        public bool showTypeBuff = true;
        public bool showTypeDebuff = true;

        public SortMode sort = SortMode.NameAsc;
    }

    private enum SortMode
    {
        NameAsc,
        NameDesc,
        CostAsc,
        CostDesc
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

        if (_filters.costEnabled)
        {
            int min = Math.Min(_filters.costMin, _filters.costMax);
            int max = Math.Max(_filters.costMin, _filters.costMax);
            query = query.Where(c => c.cost >= min && c.cost <= max);
        }

        if (_filters.upgradedOnly)
            query = query.Where(c => c.isUpgraded);

        if (_filters.baseOnly)
            query = query.Where(c => !c.isUpgraded);

        if (_filters.minionsOnly)
            query = query.Where(IsMinionLike);

        if (_filters.noDescriptionsOnly)
            query = query.Where(c => string.IsNullOrWhiteSpace(c.desc));

        query = query.Where(c => MatchesTypeFilter(c.type));

        query = _filters.sort switch
        {
            SortMode.NameAsc => query.OrderBy(c => DisplayName(c)),
            SortMode.NameDesc => query.OrderByDescending(c => DisplayName(c)),
            SortMode.CostAsc => query.OrderBy(c => c.cost).ThenBy(c => DisplayName(c)),
            SortMode.CostDesc => query.OrderByDescending(c => c.cost).ThenBy(c => DisplayName(c)),
            _ => query
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
                _filters.sort = (SortMode)EditorGUILayout.EnumPopup("Sort", _filters.sort);
            }

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool nextCostEnabled = EditorGUILayout.ToggleLeft("Filter Cost", _filters.costEnabled, GUILayout.Width(100));
                int nextMin = EditorGUILayout.IntField(_filters.costMin, GUILayout.Width(44));
                GUILayout.Label("to", GUILayout.Width(16));
                int nextMax = EditorGUILayout.IntField(_filters.costMax, GUILayout.Width(44));

                if (nextCostEnabled != _filters.costEnabled || nextMin != _filters.costMin || nextMax != _filters.costMax)
                {
                    _filters.costEnabled = nextCostEnabled;
                    _filters.costMin = nextMin;
                    _filters.costMax = nextMax;
                    ApplyFilters();
                    SavePrefs();
                }
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
        }
    }

    private void DrawCardList()
    {
        if (_visibleCards.Count == 0)
        {
            EditorGUILayout.HelpBox("No cards match the current filters.", MessageType.Info);
            return;
        }

        for (int i = 0; i < _visibleCards.Count; i++)
        {
            CardSO card = _visibleCards[i];
            if (card == null)
                continue;

            bool isSelected = card == _selected;

            using (new EditorGUILayout.HorizontalScope())
            {
                Texture icon = null;
                if (card.cardArt != null)
                    icon = AssetPreview.GetAssetPreview(card.cardArt) ?? AssetPreview.GetMiniThumbnail(card.cardArt);
                if (icon == null)
                    icon = AssetPreview.GetMiniThumbnail(card);

                GUIContent content = new GUIContent($"{DisplayName(card)}   (Cost {card.cost})", icon);

                Rect r = GUILayoutUtility.GetRect(content, _listItemStyle, GUILayout.Height(20));
                if (isSelected)
                    EditorGUI.DrawRect(r, new Color(0.2f, 0.4f, 0.85f, 0.25f));

                GUIStyle whiteText = new GUIStyle(GUIStyle.none);
                whiteText.normal.textColor = Color.white;
                whiteText.hover.textColor = Color.white;

                if (GUI.Button(r, content, whiteText))
                    Select(card);

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

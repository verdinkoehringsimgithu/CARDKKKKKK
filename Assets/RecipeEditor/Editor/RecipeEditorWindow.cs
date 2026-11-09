using System;
using System.Collections.Generic;
using System.IO;
using CardKKK.RecipeEditor.Core;
using CardKKK.RecipeEditor.Runtime;
using UnityEditor;
using UnityEngine;

namespace CardKKK.RecipeEditor.Editor
{
    /// <summary>
    /// 游戏配方编辑器窗口。
    /// 涵盖：物品库、可复用标签池、合成场所（含物品作为场所）、
    /// 合成配方（等式左右不限容量、自身转变配方、在场操作个体）、导入导出与数据校验。
    /// </summary>
    public sealed class RecipeEditorWindow : EditorWindow
    {
        [MenuItem("卡牌桌面/配方编辑器", priority = 200)]
        [MenuItem("工具/配方编辑器", priority = 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<RecipeEditorWindow>("配方编辑器");
            window.minSize = new Vector2(850, 550);
            window.Show();
        }

        [SerializeField] private RecipeDatabaseAsset _activeAsset;
        [SerializeField] private RecipeDatabaseData _database = new RecipeDatabaseData();

        private int _selectedTab = 0;
        private readonly string[] _tabNames = new string[] { "物品库", "标签池", "合成场所", "配方管理", "概览与校验" };

        // 列表选择索引
        private int _selectedItemIndex = -1;
        private int _selectedTagIndex = -1;
        private int _selectedLocationIndex = -1;
        private int _selectedRecipeIndex = -1;
        private int _selectedAuxIndex = -1;
        private int _selectedAbilityIndex = -1;

        // 搜索关键词
        private string _abilitySearch = "";
        private string _itemSearch = "";
        private string _tagSearch = "";
        private string _locationSearch = "";
        private string _recipeSearch = "";
        private string _auxSearch = "";

        // 筛选
        private int _recipeTypeFilter = 0; // 0: 全部, 1: 标准合成, 2: 自身转变
        private string _recipeLocationFilter = "";

        // 滚动位置
        private Vector2 _leftListScroll;
        private Vector2 _rightDetailScroll;

        // 快速创建临时字段
        private string _quickNewTagName = "";
        private string _statusMessage = "就绪";
        private List<ValidationMessage> _lastValidationResults = new List<ValidationMessage>();

        // 拆包游戏参考展台数据与选择状态（单独页签展示，不污染当前正在编辑的工程库）
        private RecipeDatabaseData _refDbTropical;
        private RecipeDatabaseData _refDbFantasy;
        private RecipeDatabaseData _refDbFreeze;
        private string _refSelectedRecipeId = "";
        private string _refSearchQuery = "";
        private int _refRecipeTypeFilter = 0; // 0: 全部, 1: 标准合成, 2: 自身转变
        private Vector2 _refLeftScroll;
        private Vector2 _refRightScroll;

        private void OnEnable()
        {
            if (_activeAsset != null)
            {
                LoadFromAsset(_activeAsset);
            }
            else
            {
                // 尝试查找工程中的默认资产
                string[] guids = AssetDatabase.FindAssets("t:RecipeDatabaseAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var asset = AssetDatabase.LoadAssetAtPath<RecipeDatabaseAsset>(path);
                    if (asset != null)
                    {
                        _activeAsset = asset;
                        LoadFromAsset(asset);
                    }
                }
            }

            if (_database == null)
            {
                _database = new RecipeDatabaseData();
            }
        }

        private void OnGUI()
        {
            DrawHeaderToolbar();
            EditorGUILayout.Space(2);

            // 主功能标签栏（第 1 行）
            int mainTabSelection = _selectedTab <= 6 ? _selectedTab : -1;
            int newMainTab = GUILayout.Toolbar(mainTabSelection, GetTabLabels(), GUILayout.Height(28));
            if (newMainTab >= 0 && newMainTab != _selectedTab)
            {
                _selectedTab = newMainTab;
                _leftListScroll = Vector2.zero;
                _rightDetailScroll = Vector2.zero;
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space(2);

            // 拆包参考展台标签栏（第 2 行，完全隔离展示，不影响主功能）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("拆包参考展台:", EditorStyles.boldLabel, GUILayout.Width(95));
            int refTabSelection = _selectedTab >= 7 ? (_selectedTab - 7) : -1;
            int newRefTab = GUILayout.Toolbar(refTabSelection, GetRefTabLabels(), GUILayout.Height(24));
            if (newRefTab >= 0 && (newRefTab + 7) != _selectedTab)
            {
                _selectedTab = newRefTab + 7;
                _refSelectedRecipeId = "";
                _refSearchQuery = "";
                _refRecipeTypeFilter = 0;
                _refLeftScroll = Vector2.zero;
                _refRightScroll = Vector2.zero;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 主工作区
            switch (_selectedTab)
            {
                case 0: DrawItemsPage(); break;
                case 1: DrawTagsPage(); break;
                case 2: DrawAbilitiesPage(); break;
                case 3: DrawLocationsPage(); break;
                case 4: DrawRecipesPage(); break;
                case 5: DrawAuxiliaryLinesPage(); break;
                case 6: DrawOverviewPage(); break;
                case 7: DrawReferenceGamePage("卡牌生存：热带岛屿", "tropical", "Assets/RecipeEditor/Data/热带岛屿配方参考.json"); break;
                case 8: DrawReferenceGamePage("卡牌生存：奇幻森林", "fantasy", "Assets/RecipeEditor/Data/奇幻森林配方参考.json"); break;
                case 9: DrawReferenceGamePage("别冻僵 (Don't Freeze)", "freeze", "Assets/RecipeEditor/Data/别冻僵配方参考.json"); break;
            }

            DrawFooterStatusBar();
        }

        private string[] GetTabLabels()
        {
            return new string[]
            {
                $"物品库 ({_database?.Items?.Count ?? 0})",
                $"标签池 ({_database?.Tags?.Count ?? 0})",
                $"能力定义 ({_database?.Abilities?.Count ?? 0})",
                $"合成场所 ({_database?.Locations?.Count ?? 0})",
                $"配方管理 ({_database?.Recipes?.Count ?? 0})",
                $"副产线 ({_database?.AuxiliaryLines?.Count ?? 0})",
                $"概览与校验 ({_lastValidationResults.Count})"
            };
        }

        #region 顶部工具栏与资产存取

        private void DrawHeaderToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 关联资产选择
            EditorGUI.BeginChangeCheck();
            var newAsset = (RecipeDatabaseAsset)EditorGUILayout.ObjectField("配方资产:", _activeAsset, typeof(RecipeDatabaseAsset), false, GUILayout.Width(260));
            if (EditorGUI.EndChangeCheck())
            {
                if (newAsset != _activeAsset)
                {
                    _activeAsset = newAsset;
                    if (_activeAsset != null)
                    {
                        LoadFromAsset(_activeAsset);
                    }
                }
            }

            if (GUILayout.Button("新建资产", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                CreateNewAsset();
            }

            GUILayout.Space(10);

            // 保存资产按钮
            if (GUILayout.Button("保存资产", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                SaveToAsset();
            }

            // 导出 JSON
            if (GUILayout.Button("导出 JSON", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                ExportToJsonFile();
            }

            // 导入 JSON
            if (GUILayout.Button("从 JSON 导入", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                ImportFromJsonFile();
            }

            GUILayout.FlexibleSpace();

            // 完整性校验按钮
            if (GUILayout.Button("校验完整性", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RunValidation();
                _selectedTab = 5; // 切换到概览与校验页
            }

            EditorGUILayout.EndHorizontal();
        }

        private void LoadFromAsset(RecipeDatabaseAsset asset)
        {
            if (asset == null || asset.Database == null) return;
            _database = asset.Database.Clone();
            _statusMessage = $"已加载资产: {asset.name}";
            RunValidation();
        }

        private void SaveToAsset()
        {
            if (_activeAsset == null)
            {
                CreateNewAsset();
                return;
            }

            _activeAsset.Database = _database.Clone();
            EditorUtility.SetDirty(_activeAsset);
            AssetDatabase.SaveAssets();
            _statusMessage = $"已于 {DateTime.Now:HH:mm:ss} 保存到资产: {_activeAsset.name}";
        }

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建配方数据库资产", "游戏配方数据库", "asset", "请选择保存路径", "Assets/RecipeEditor/Data");
            if (string.IsNullOrEmpty(path)) return;

            var newAsset = CreateInstance<RecipeDatabaseAsset>();
            newAsset.Database = _database.Clone();
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();
            _activeAsset = newAsset;
            _statusMessage = $"新资产已创建: {path}";
        }

        private void ExportToJsonFile()
        {
            string defaultName = string.IsNullOrEmpty(_database.DatabaseName) ? "配方数据库" : _database.DatabaseName;
            string path = EditorUtility.SaveFilePanel("导出配方数据为 JSON", Application.dataPath, $"{defaultName}.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            if (RecipeJsonSerializer.ExportToFile(_database, path, out string error))
            {
                _statusMessage = $"成功导出到: {path}";
                EditorUtility.DisplayDialog("导出成功", $"配方数据库已成功导出至:\n{path}", "确定");
            }
            else
            {
                _statusMessage = $"导出失败: {error}";
                EditorUtility.DisplayDialog("导出失败", error, "确定");
            }
        }

        private void ImportFromJsonFile()
        {
            string path = EditorUtility.OpenFilePanel("从 JSON 文件导入配方数据", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            var imported = RecipeJsonSerializer.ImportFromFile(path, out string error);
            if (imported == null)
            {
                EditorUtility.DisplayDialog("导入失败", error, "确定");
                return;
            }

            int option = EditorUtility.DisplayDialogComplex("导入模式选择",
                $"已读取外部数据:\n- 物品: {imported.Items?.Count ?? 0}\n- 标签: {imported.Tags?.Count ?? 0}\n- 场所: {imported.Locations?.Count ?? 0}\n- 配方: {imported.Recipes?.Count ?? 0}\n\n请选择导入方式：",
                "完全覆盖当前数据",
                "取消",
                "增量合并到当前数据");

            if (option == 0) // 完全覆盖
            {
                _database = imported.Clone();
                _selectedItemIndex = -1;
                _selectedTagIndex = -1;
                _selectedLocationIndex = -1;
                _selectedRecipeIndex = -1;
                _statusMessage = $"已从文件完全覆盖导入: {path}";
                RunValidation();
            }
            else if (option == 2) // 增量合并
            {
                var report = RecipeDataMerger.Merge(_database, imported, MergeConflictPolicy.Overwrite);
                _statusMessage = $"增量合并完成: {report}";
                RunValidation();
                EditorUtility.DisplayDialog("合并完成", report.ToString(), "确定");
            }
        }

        private void RunValidation()
        {
            _lastValidationResults = RecipeValidator.Validate(_database);
        }

        #endregion

        #region 页面 1: 物品库 (Items)

        private void DrawItemsPage()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：物品列表 (宽 280)
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _itemSearch = EditorGUILayout.TextField(_itemSearch, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40))) _itemSearch = "";
            EditorGUILayout.EndHorizontal();

            _leftListScroll = EditorGUILayout.BeginScrollView(_leftListScroll, EditorStyles.helpBox);
            if (_database.Items != null)
            {
                for (int i = 0; i < _database.Items.Count; i++)
                {
                    var item = _database.Items[i];
                    if (item == null) continue;

                    // 搜索过滤
                    if (!string.IsNullOrEmpty(_itemSearch))
                    {
                        bool match = (!string.IsNullOrEmpty(item.Name) && item.Name.IndexOf(_itemSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(item.Id) && item.Id.IndexOf(_itemSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(item.Category) && item.Category.IndexOf(_itemSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    bool isSelected = (_selectedItemIndex == i);
                    bool isLocation = _database.FindLocationByBoundItem(item.Id) != null;

                    EditorGUILayout.BeginHorizontal(isSelected ? RecipeEditorStyles.CardBox : EditorStyles.helpBox);

                    string displayName = string.IsNullOrEmpty(item.Name) ? item.Id : item.Name;
                    if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Height(22)))
                    {
                        _selectedItemIndex = i;
                        GUI.FocusControl(null);
                    }

                    if (isLocation)
                    {
                        RecipeEditorStyles.DrawBadge("场所", new Color(0.2f, 0.7f, 0.3f));
                    }

                    EditorGUILayout.LabelField(item.Category ?? "", EditorStyles.miniLabel, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 新建物品"))
            {
                AddNewItem();
            }
            if (_selectedItemIndex >= 0 && _selectedItemIndex < (_database.Items?.Count ?? 0))
            {
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _database.Items.RemoveAt(_selectedItemIndex);
                    _selectedItemIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 右侧：物品详情编辑
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);
            if (_selectedItemIndex >= 0 && _selectedItemIndex < (_database.Items?.Count ?? 0))
            {
                DrawItemDetail(_database.Items[_selectedItemIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox("请在左侧选择一个物品，或点击“+ 新建物品”开始编辑。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewItem()
        {
            if (_database.Items == null) _database.Items = new List<RecipeItem>();
            string newId = "item_" + (_database.Items.Count + 1);
            int idx = 1;
            while (_database.FindItem(newId) != null)
            {
                newId = $"item_{_database.Items.Count + idx++}";
            }

            var newItem = new RecipeItem(newId, "新物品", "材料", "");
            _database.Items.Add(newItem);
            _selectedItemIndex = _database.Items.Count - 1;
        }

        private void DrawItemDetail(RecipeItem item)
        {
            RecipeEditorStyles.DrawSectionHeader("物品基本信息", "配置该物品的全局标识、名称与堆叠");

            item.Id = EditorGUILayout.TextField("物品 ID (唯一):", item.Id);
            item.Name = EditorGUILayout.TextField("显示名称:", item.Name);
            item.Category = EditorGUILayout.TextField("物品分类:", item.Category);
            item.MaxStack = EditorGUILayout.IntField("最大堆叠数:", item.MaxStack);

            EditorGUILayout.LabelField("物品描述 / 备注:");
            item.Description = EditorGUILayout.TextArea(item.Description, GUILayout.Height(45));

            EditorGUILayout.Space(8);

            // ==================== 标签复用绑定与快捷管理 ====================
            RecipeEditorStyles.DrawSectionHeader("复用标签绑定", "为物品关联标签池中的标签，可供配方按标签统一匹配");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);

            // 展现当前持有的标签
            if (item.TagIds != null && item.TagIds.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("当前持有标签:", GUILayout.Width(90));
                for (int t = 0; t < item.TagIds.Count; t++)
                {
                    string tagId = item.TagIds[t];
                    var tag = _database.FindTag(tagId);
                    string tagName = tag != null ? tag.Name : tagId;
                    string colorHex = tag != null ? tag.ColorHex : "#888888";

                    if (RecipeEditorStyles.DrawTagBadge(tagName, colorHex, true, out bool removeClicked))
                    {
                        if (removeClicked)
                        {
                            item.TagIds.RemoveAt(t);
                            break;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("该物品尚未添加任何标签。", MessageType.None);
            }

            EditorGUILayout.Space(4);

            // 从标签池选择添加
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("+ 从复用池添加:", GUILayout.Width(100));

            var availableTags = new List<RecipeTag>();
            if (_database.Tags != null)
            {
                foreach (var tg in _database.Tags)
                {
                    if (tg != null && !item.HasTag(tg.Id)) availableTags.Add(tg);
                }
            }

            if (availableTags.Count > 0)
            {
                string[] tagOptions = new string[availableTags.Count + 1];
                tagOptions[0] = "-- 选择已有标签添加 --";
                for (int i = 0; i < availableTags.Count; i++)
                {
                    tagOptions[i + 1] = $"{availableTags[i].Name} ({availableTags[i].Id})";
                }

                int chosen = EditorGUILayout.Popup(0, tagOptions);
                if (chosen > 0)
                {
                    item.AddTag(availableTags[chosen - 1].Id);
                }
            }
            else
            {
                EditorGUILayout.LabelField("(暂无其他可用标签)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // 快速新建并复用标签
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("+ 新建并加入池:", GUILayout.Width(100));
            _quickNewTagName = EditorGUILayout.TextField(_quickNewTagName);
            if (GUILayout.Button("创建并绑定", GUILayout.Width(85)))
            {
                if (!string.IsNullOrWhiteSpace(_quickNewTagName))
                {
                    string newTagId = "tag_" + _quickNewTagName.ToLower();
                    // 检查是否已存在
                    var existingTag = _database.FindTag(newTagId);
                    if (existingTag == null)
                    {
                        if (_database.Tags == null) _database.Tags = new List<RecipeTag>();
                        existingTag = new RecipeTag(newTagId, _quickNewTagName, "#27AE60", "常规");
                        _database.Tags.Add(existingTag);
                    }
                    item.AddTag(existingTag.Id);
                    _quickNewTagName = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ==================== 物品作为合成场所支持 ====================
            RecipeEditorStyles.DrawSectionHeader("合成场所属性", "是否将此物品作为配方的合成场所（发生地）");

            var boundLocation = _database.FindLocationByBoundItem(item.Id);
            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);

            if (boundLocation != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox($"✔ 该物品当前已作为合成场所使用：【{boundLocation.Name}】(工位容量: {boundLocation.Capacity})", MessageType.Info);
                if (GUILayout.Button("前往场所设置", GUILayout.Width(95), GUILayout.Height(30)))
                {
                    _selectedLocationIndex = _database.Locations.IndexOf(boundLocation);
                    _selectedTab = 2; // 切换到场所标签页
                }
                if (GUILayout.Button("取消场所身份", GUILayout.Width(95), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("确认取消", $"确定将场所【{boundLocation.Name}】移除吗？", "确定", "取消"))
                    {
                        _database.Locations.Remove(boundLocation);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("当前未作为合成场所。点击右侧按钮可一键将其转为合成发生地：", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("★ 设为合成场所", GUILayout.Width(110), GUILayout.Height(26)))
                {
                    var newLoc = _database.AddItemAsLocation(item.Id, capacity: 1);
                    if (newLoc != null)
                    {
                        _statusMessage = $"已将物品 [{item.Name}] 注册为合成场所！";
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ==================== 自定义数值与属性 ====================
            RecipeEditorStyles.DrawSectionHeader("扩展数值与属性", "例如耐久度、热值、营养度等自定义数值");

            if (item.Properties == null) item.Properties = new List<RecipePropertyEntry>();
            for (int p = 0; p < item.Properties.Count; p++)
            {
                var prop = item.Properties[p];
                if (prop == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("属性键:", GUILayout.Width(50));
                prop.Key = EditorGUILayout.TextField(prop.Key, GUILayout.Width(120));
                EditorGUILayout.LabelField("数值/文本:", GUILayout.Width(65));
                prop.Value = EditorGUILayout.TextField(prop.Value);

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    item.Properties.RemoveAt(p);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 添加自定义属性", GUILayout.Width(130)))
            {
                item.Properties.Add(new RecipePropertyEntry("durability", "100"));
            }

            EditorGUILayout.Space(8);

            // ==================== 默认能力点数与换算 ====================
            RecipeEditorStyles.DrawSectionHeader("默认能力点数与换算", "卡牌默认携带的点数与投入时的折算率（不填即为0点，默认100%投入换算）");

            if (item.AbilityContributions == null) item.AbilityContributions = new List<ItemAbilityContribution>();

            for (int a = 0; a < item.AbilityContributions.Count; a++)
            {
                var contrib = item.AbilityContributions[a];
                if (contrib == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                // 能力选择下拉
                string[] abilityOptions = GetAbilityDropdownOptions(out List<string> abIds);
                int curAbIdx = abIds.IndexOf(contrib.AbilityId ?? "");
                int chosenAb = EditorGUILayout.Popup(Math.Max(0, curAbIdx), abilityOptions, GUILayout.Width(140));
                if (chosenAb >= 0 && chosenAb < abIds.Count)
                {
                    contrib.AbilityId = abIds[chosenAb];
                }

                EditorGUILayout.LabelField("默认点数:", GUILayout.Width(60));
                contrib.BaseValue = EditorGUILayout.FloatField(contrib.BaseValue, GUILayout.Width(50));

                EditorGUILayout.LabelField("折算率%:", GUILayout.Width(55));
                float efficiencyPercent = contrib.Efficiency * 100f;
                efficiencyPercent = EditorGUILayout.FloatField(efficiencyPercent, GUILayout.Width(45));
                contrib.Efficiency = Math.Max(0.01f, efficiencyPercent / 100f);

                EditorGUILayout.LabelField("跨能力折算目标:", GUILayout.Width(95));
                contrib.TargetAbilityId = EditorGUILayout.TextField(contrib.TargetAbilityId, GUILayout.Width(80));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    item.AbilityContributions.RemoveAt(a);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("说明:", GUILayout.Width(35));
                contrib.Note = EditorGUILayout.TextField(contrib.Note);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ 添加携带能力点数", GUILayout.Width(150)))
            {
                string defaultAb = _database.Abilities != null && _database.Abilities.Count > 0 ? _database.Abilities[0].Id : "cooking";
                item.AbilityContributions.Add(new ItemAbilityContribution(defaultAb, 5f, 1.0f));
            }
        }

        #endregion

        #region 页面 2: 标签池 (Tags)

        private void DrawTagsPage()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：标签列表
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _tagSearch = EditorGUILayout.TextField(_tagSearch, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40))) _tagSearch = "";
            EditorGUILayout.EndHorizontal();

            _leftListScroll = EditorGUILayout.BeginScrollView(_leftListScroll, EditorStyles.helpBox);
            if (_database.Tags != null)
            {
                for (int i = 0; i < _database.Tags.Count; i++)
                {
                    var tag = _database.Tags[i];
                    if (tag == null) continue;

                    if (!string.IsNullOrEmpty(_tagSearch))
                    {
                        bool match = (!string.IsNullOrEmpty(tag.Name) && tag.Name.IndexOf(_tagSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(tag.Id) && tag.Id.IndexOf(_tagSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    bool isSelected = (_selectedTagIndex == i);
                    EditorGUILayout.BeginHorizontal(isSelected ? RecipeEditorStyles.CardBox : EditorStyles.helpBox);

                    int usageCount = _database.GetTagUsageCount(tag.Id);
                    string displayName = string.IsNullOrEmpty(tag.Name) ? tag.Id : tag.Name;

                    if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Height(22)))
                    {
                        _selectedTagIndex = i;
                        GUI.FocusControl(null);
                    }

                    // 颜色小方块
                    Color tagCol;
                    if (ColorUtility.TryParseHtmlString(tag.ColorHex, out tagCol))
                    {
                        RecipeEditorStyles.DrawBadge($"{usageCount}用", tagCol);
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge($"{usageCount}用", Color.gray);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 新建标签"))
            {
                AddNewTag();
            }
            if (_selectedTagIndex >= 0 && _selectedTagIndex < (_database.Tags?.Count ?? 0))
            {
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _database.Tags.RemoveAt(_selectedTagIndex);
                    _selectedTagIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 右侧：标签详情编辑
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);
            if (_selectedTagIndex >= 0 && _selectedTagIndex < (_database.Tags?.Count ?? 0))
            {
                DrawTagDetail(_database.Tags[_selectedTagIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox("请在左侧选择一个标签，或点击“+ 新建标签”进行配置。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewTag()
        {
            if (_database.Tags == null) _database.Tags = new List<RecipeTag>();
            string newId = "tag_" + (_database.Tags.Count + 1);
            int idx = 1;
            while (_database.FindTag(newId) != null)
            {
                newId = $"tag_{_database.Tags.Count + idx++}";
            }

            var newTag = new RecipeTag(newId, "新标签", "#3498DB", "常规", "");
            _database.Tags.Add(newTag);
            _selectedTagIndex = _database.Tags.Count - 1;
        }

        private void DrawTagDetail(RecipeTag tag)
        {
            RecipeEditorStyles.DrawSectionHeader("标签详情与显示设置", "配置全局复用标签的 ID、名称与辨识颜色");

            tag.Id = EditorGUILayout.TextField("标签 ID (唯一):", tag.Id);
            tag.Name = EditorGUILayout.TextField("显示名称:", tag.Name);
            tag.Category = EditorGUILayout.TextField("标签分类:", tag.Category);

            // 颜色选择器
            Color tagColor;
            if (!ColorUtility.TryParseHtmlString(tag.ColorHex, out tagColor))
            {
                tagColor = new Color(0.2f, 0.6f, 0.9f);
            }
            EditorGUI.BeginChangeCheck();
            tagColor = EditorGUILayout.ColorField("展示颜色:", tagColor);
            if (EditorGUI.EndChangeCheck())
            {
                tag.ColorHex = "#" + ColorUtility.ToHtmlStringRGB(tagColor);
            }

            EditorGUILayout.LabelField("标签说明:");
            tag.Description = EditorGUILayout.TextArea(tag.Description, GUILayout.Height(50));

            EditorGUILayout.Space(12);

            // 引用此标签的物品列表
            RecipeEditorStyles.DrawSectionHeader("引用此标签的物品列表");
            int count = 0;
            if (_database.Items != null)
            {
                foreach (var it in _database.Items)
                {
                    if (it != null && it.HasTag(tag.Id))
                    {
                        count++;
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"• {it.Name} ({it.Id})", EditorStyles.boldLabel);
                        if (GUILayout.Button("跳转物品", GUILayout.Width(75)))
                        {
                            _selectedItemIndex = _database.Items.IndexOf(it);
                            _selectedTab = 0;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox("当前尚无任何物品引用此标签。", MessageType.None);
            }
        }

        #endregion

        #region 页面 3: 能力定义池 (Abilities)

        private void DrawAbilitiesPage()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：能力列表
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _abilitySearch = EditorGUILayout.TextField(_abilitySearch, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40))) _abilitySearch = "";
            EditorGUILayout.EndHorizontal();

            _leftListScroll = EditorGUILayout.BeginScrollView(_leftListScroll, EditorStyles.helpBox);
            if (_database.Abilities != null)
            {
                for (int i = 0; i < _database.Abilities.Count; i++)
                {
                    var ab = _database.Abilities[i];
                    if (ab == null) continue;

                    if (!string.IsNullOrEmpty(_abilitySearch))
                    {
                        bool match = (!string.IsNullOrEmpty(ab.Name) && ab.Name.IndexOf(_abilitySearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(ab.Id) && ab.Id.IndexOf(_abilitySearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(ab.Category) && ab.Category.IndexOf(_abilitySearch, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    bool isSelected = (_selectedAbilityIndex == i);
                    EditorGUILayout.BeginHorizontal(isSelected ? RecipeEditorStyles.CardBox : EditorStyles.helpBox);

                    int usageCount = _database.GetAbilityUsageCount(ab.Id);
                    string displayName = $"{ab.IconOrEmoji} {(string.IsNullOrEmpty(ab.Name) ? ab.Id : ab.Name)}";

                    if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Height(22)))
                    {
                        _selectedAbilityIndex = i;
                        GUI.FocusControl(null);
                    }

                    if (usageCount > 0)
                    {
                        RecipeEditorStyles.DrawBadge($"{usageCount}用", new Color(0.2f, 0.5f, 0.8f));
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 新建能力"))
            {
                AddNewAbility();
            }
            if (_selectedAbilityIndex >= 0 && _selectedAbilityIndex < (_database.Abilities?.Count ?? 0))
            {
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _database.Abilities.RemoveAt(_selectedAbilityIndex);
                    _selectedAbilityIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 右侧：能力详情编辑
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);
            if (_selectedAbilityIndex >= 0 && _selectedAbilityIndex < (_database.Abilities?.Count ?? 0))
            {
                DrawAbilityDetail(_database.Abilities[_selectedAbilityIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox("请在左侧选择一个能力定义，或点击“+ 新建能力”开始配置。\n支持配置烹饪技艺、冶炼、火力热量、体能力量、感知等配方检定与卡牌点数类型。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewAbility()
        {
            if (_database.Abilities == null) _database.Abilities = new List<RecipeAbility>();
            string newId = "ability_" + (_database.Abilities.Count + 1);
            int idx = 1;
            while (_database.FindAbility(newId) != null)
            {
                newId = $"ability_{_database.Abilities.Count + idx++}";
            }

            var newAb = new RecipeAbility(newId, "新能力属性", "通用属性", "用于卡牌能力点数与配方检定标准", "⚡", "#3498DB");
            _database.Abilities.Add(newAb);
            _selectedAbilityIndex = _database.Abilities.Count - 1;
        }

        private void DrawAbilityDetail(RecipeAbility ab)
        {
            RecipeEditorStyles.DrawSectionHeader("能力基础信息", "定义全局能力标尺，供卡牌携带点数及配方检定统一引用");

            ab.Id = EditorGUILayout.TextField("能力 ID (唯一):", ab.Id);
            ab.Name = EditorGUILayout.TextField("显示名称:", ab.Name);
            ab.Category = EditorGUILayout.TextField("能力分类:", ab.Category);
            ab.IconOrEmoji = EditorGUILayout.TextField("图标 / Emoji:", ab.IconOrEmoji);
            ab.ColorHex = EditorGUILayout.TextField("色彩色值 (十六进制):", ab.ColorHex);

            EditorGUILayout.LabelField("能力说明 / 检定含义:");
            ab.Description = EditorGUILayout.TextArea(ab.Description, GUILayout.Height(50));

            EditorGUILayout.Space(12);

            // 统计引用
            int usageCount = _database.GetAbilityUsageCount(ab.Id);
            EditorGUILayout.HelpBox($"当前全库共有 {usageCount} 处卡牌携带点数或配方检定引用了本能力。", MessageType.None);
        }

        private string[] GetAbilityDropdownOptions(out List<string> abIds)
        {
            abIds = new List<string>();
            var options = new List<string>();

            if (_database.Abilities != null)
            {
                foreach (var ab in _database.Abilities)
                {
                    if (ab != null && !string.IsNullOrEmpty(ab.Id))
                    {
                        abIds.Add(ab.Id);
                        options.Add($"{ab.IconOrEmoji} {ab.Name} ({ab.Id})");
                    }
                }
            }

            if (abIds.Count == 0)
            {
                abIds.Add("");
                options.Add("-- 暂无能力，请在能力定义页创建 --");
            }

            return options.ToArray();
        }

        #endregion

        #region 页面 4: 合成场所库 (Locations)

        private void DrawLocationsPage()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：场所列表
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _locationSearch = EditorGUILayout.TextField(_locationSearch, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40))) _locationSearch = "";
            EditorGUILayout.EndHorizontal();

            _leftListScroll = EditorGUILayout.BeginScrollView(_leftListScroll, EditorStyles.helpBox);
            if (_database.Locations != null)
            {
                for (int i = 0; i < _database.Locations.Count; i++)
                {
                    var loc = _database.Locations[i];
                    if (loc == null) continue;

                    if (!string.IsNullOrEmpty(_locationSearch))
                    {
                        bool match = (!string.IsNullOrEmpty(loc.Name) && loc.Name.IndexOf(_locationSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(loc.Id) && loc.Id.IndexOf(_locationSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    bool isSelected = (_selectedLocationIndex == i);
                    EditorGUILayout.BeginHorizontal(isSelected ? RecipeEditorStyles.CardBox : EditorStyles.helpBox);

                    string displayName = string.IsNullOrEmpty(loc.Name) ? loc.Id : loc.Name;
                    if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Height(22)))
                    {
                        _selectedLocationIndex = i;
                        GUI.FocusControl(null);
                    }

                    if (loc.SourceType == LocationSourceType.Item)
                    {
                        RecipeEditorStyles.DrawBadge("物品", new Color(0.2f, 0.7f, 0.3f));
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge("独立", new Color(0.3f, 0.5f, 0.8f));
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 独立场所"))
            {
                AddNewLocation(LocationSourceType.Custom);
            }
            if (GUILayout.Button("+ 物品场所"))
            {
                ShowAddItemAsLocationMenu();
            }
            if (_selectedLocationIndex >= 0 && _selectedLocationIndex < (_database.Locations?.Count ?? 0))
            {
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _database.Locations.RemoveAt(_selectedLocationIndex);
                    _selectedLocationIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 右侧：场所详情编辑
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);
            if (_selectedLocationIndex >= 0 && _selectedLocationIndex < (_database.Locations?.Count ?? 0))
            {
                DrawLocationDetail(_database.Locations[_selectedLocationIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox("请在左侧选择一个合成场所，或点击下方按钮添加场所。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewLocation(LocationSourceType sourceType)
        {
            if (_database.Locations == null) _database.Locations = new List<RecipeLocation>();
            string newId = "loc_" + (_database.Locations.Count + 1);
            int idx = 1;
            while (_database.FindLocation(newId) != null)
            {
                newId = $"loc_{_database.Locations.Count + idx++}";
            }

            var newLoc = new RecipeLocation(newId, "新合成场所", "", capacity: 1)
            {
                SourceType = sourceType
            };
            _database.Locations.Add(newLoc);
            _selectedLocationIndex = _database.Locations.Count - 1;
        }

        private void ShowAddItemAsLocationMenu()
        {
            var menu = new GenericMenu();
            if (_database.Items == null || _database.Items.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("当前物品库为空，请先在物品库中创建物品"));
            }
            else
            {
                foreach (var item in _database.Items)
                {
                    if (item == null) continue;
                    string itemId = item.Id;
                    string itemName = item.Name;
                    bool alreadyLoc = _database.FindLocationByBoundItem(itemId) != null;

                    if (alreadyLoc)
                    {
                        menu.AddDisabledItem(new GUIContent($"{itemName} (已是合成场所)"));
                    }
                    else
                    {
                        menu.AddItem(new GUIContent($"添加物品: {itemName}"), false, () =>
                        {
                            var loc = _database.AddItemAsLocation(itemId);
                            if (loc != null)
                            {
                                _selectedLocationIndex = _database.Locations.IndexOf(loc);
                                _statusMessage = $"已添加物品 [{itemName}] 为合成场所";
                            }
                        });
                    }
                }
            }
            menu.ShowAsContext();
        }

        private void DrawLocationDetail(RecipeLocation loc)
        {
            RecipeEditorStyles.DrawSectionHeader("合成场所属性", "配方的发生地设置，支持独立场所与实体物品场所");

            loc.Id = EditorGUILayout.TextField("场所 ID (唯一):", loc.Id);
            loc.Name = EditorGUILayout.TextField("场所名称:", loc.Name);
            loc.Capacity = EditorGUILayout.IntField("工位容量 (并发合成数):", Math.Max(1, loc.Capacity));
            loc.DurabilityCost = EditorGUILayout.IntField("设施单次损耗度 (磨损):", Math.Max(0, loc.DurabilityCost));
            EditorGUILayout.HelpBox("工位设施具备耐久损耗/燃料消耗机制。当前暂不设损耗数值时请保持为 0（0 代表暂不设损耗）。", MessageType.None);

            // 来源类型切换
            loc.SourceType = (LocationSourceType)EditorGUILayout.EnumPopup("场所来源类型:", loc.SourceType);

            if (loc.SourceType == LocationSourceType.Item)
            {
                EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
                EditorGUILayout.LabelField("绑定的物品卡牌:", EditorStyles.boldLabel);

                // 物品下拉选择
                string[] itemOptions = GetItemDropdownOptions(out List<string> itemIds);
                int currentItemIndex = itemIds.IndexOf(loc.BoundItemId);
                int selectedItem = EditorGUILayout.Popup("关联物品:", Math.Max(0, currentItemIndex), itemOptions);
                if (selectedItem >= 0 && selectedItem < itemIds.Count)
                {
                    loc.BoundItemId = itemIds[selectedItem];
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"当前绑定物品 ID: {loc.BoundItemId}", EditorStyles.miniLabel);
                if (GUILayout.Button("查看物品", GUILayout.Width(75)))
                {
                    var item = _database.FindItem(loc.BoundItemId);
                    if (item != null)
                    {
                        _selectedItemIndex = _database.Items.IndexOf(item);
                        _selectedTab = 0;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.LabelField("场所描述 / 备注:");
            loc.Description = EditorGUILayout.TextArea(loc.Description, GUILayout.Height(45));

            EditorGUILayout.Space(12);

            // 在此发生的配方列表
            RecipeEditorStyles.DrawSectionHeader("在此场所发生的配方列表");
            var recipesHere = _database.GetRecipesAtLocation(loc.Id);
            if (recipesHere.Count > 0)
            {
                foreach (var r in recipesHere)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"• {r.Name} ({r.Id})", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(r.Type == RecipeType.Transformation ? "[转变配方]" : "[合成配方]", EditorStyles.miniLabel, GUILayout.Width(75));

                    if (GUILayout.Button("跳转配方", GUILayout.Width(75)))
                    {
                        _selectedRecipeIndex = _database.Recipes.IndexOf(r);
                        _selectedTab = 3;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("当前尚无配方将此场所作为发生地。", MessageType.None);
            }
        }

        #endregion

        #region 页面 4: 配方管理 (Recipes)

        private void DrawRecipesPage()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：配方列表 (宽 300)
            EditorGUILayout.BeginVertical(GUILayout.Width(300));

            // 搜索与类型过滤
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _recipeSearch = EditorGUILayout.TextField(_recipeSearch, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(35))) _recipeSearch = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _recipeTypeFilter = EditorGUILayout.Popup(_recipeTypeFilter, new string[] { "全部类型", "标准合成", "自身转变" }, EditorStyles.toolbarDropDown);
            EditorGUILayout.EndHorizontal();

            _leftListScroll = EditorGUILayout.BeginScrollView(_leftListScroll, EditorStyles.helpBox);
            if (_database.Recipes != null)
            {
                for (int i = 0; i < _database.Recipes.Count; i++)
                {
                    var recipe = _database.Recipes[i];
                    if (recipe == null) continue;

                    // 过滤
                    if (_recipeTypeFilter == 1 && recipe.Type != RecipeType.Synthesis) continue;
                    if (_recipeTypeFilter == 2 && recipe.Type != RecipeType.Transformation) continue;

                    if (!string.IsNullOrEmpty(_recipeSearch))
                    {
                        bool match = (!string.IsNullOrEmpty(recipe.Name) && recipe.Name.IndexOf(_recipeSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                  || (!string.IsNullOrEmpty(recipe.Id) && recipe.Id.IndexOf(_recipeSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    bool isSelected = (_selectedRecipeIndex == i);
                    EditorGUILayout.BeginHorizontal(isSelected ? RecipeEditorStyles.CardBox : EditorStyles.helpBox);

                    string displayName = string.IsNullOrEmpty(recipe.Name) ? recipe.Id : recipe.Name;
                    if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Height(22)))
                    {
                        _selectedRecipeIndex = i;
                        GUI.FocusControl(null);
                    }

                    // 标签徽章
                    if (recipe.Type == RecipeType.Transformation)
                    {
                        RecipeEditorStyles.DrawBadge("转变", new Color(0.9f, 0.5f, 0.2f));
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge("合成", new Color(0.2f, 0.6f, 0.9f));
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 新建合成"))
            {
                AddNewRecipe(RecipeType.Synthesis);
            }
            if (GUILayout.Button("+ 新建转变"))
            {
                AddNewRecipe(RecipeType.Transformation);
            }
            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < (_database.Recipes?.Count ?? 0))
            {
                if (GUILayout.Button("删除", GUILayout.Width(45)))
                {
                    _database.Recipes.RemoveAt(_selectedRecipeIndex);
                    _selectedRecipeIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 右侧：配方详情编辑
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);
            if (_selectedRecipeIndex >= 0 && _selectedRecipeIndex < (_database.Recipes?.Count ?? 0))
            {
                DrawRecipeDetail(_database.Recipes[_selectedRecipeIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox("请在左侧选择一个配方，或点击下方按钮添加配方。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewRecipe(RecipeType type)
        {
            if (_database.Recipes == null) _database.Recipes = new List<RecipeDefinition>();
            string prefix = type == RecipeType.Transformation ? "trans_" : "recipe_";
            string newId = prefix + (_database.Recipes.Count + 1);
            int idx = 1;
            while (_database.FindRecipe(newId) != null)
            {
                newId = $"{prefix}{_database.Recipes.Count + idx++}";
            }

            var newRecipe = new RecipeDefinition(newId, type == RecipeType.Transformation ? "新自身转变配方" : "新合成配方", type);

            // 预设一个示例项
            if (type == RecipeType.Transformation)
            {
                newRecipe.SourceQuantity = 1;
                newRecipe.TargetQuantity = 1;
            }
            else
            {
                newRecipe.Inputs.Add(new RecipeInputSlot(IngredientMatchType.ExactItem, "", 1, true));
                newRecipe.Outputs.Add(new RecipeOutputSlot("", 1, 1f));
            }

            _database.Recipes.Add(newRecipe);
            _selectedRecipeIndex = _database.Recipes.Count - 1;
        }

        private void DrawRecipeDetail(RecipeDefinition recipe)
        {
            RecipeEditorStyles.DrawSectionHeader("配方基础属性", "配置发生场所、制作耗时与配方类型");

            recipe.Id = EditorGUILayout.TextField("配方 ID (唯一):", recipe.Id);
            recipe.Name = EditorGUILayout.TextField("配方名称:", recipe.Name);
            recipe.DurationMinutes = EditorGUILayout.IntField("制作耗时 (分钟):", Math.Max(0, recipe.DurationMinutes));

            // 发生场所选择（支持独立场所与物品场所）
            string[] locationOptions = GetLocationDropdownOptions(out List<string> locIds);
            int currentLocIndex = locIds.IndexOf(recipe.LocationId ?? "");
            int chosenLoc = EditorGUILayout.Popup("发生场所:", Math.Max(0, currentLocIndex), locationOptions);
            if (chosenLoc >= 0 && chosenLoc < locIds.Count)
            {
                recipe.LocationId = locIds[chosenLoc];
            }

            // 配方类型切换
            recipe.Type = (RecipeType)EditorGUILayout.EnumPopup("配方模式:", recipe.Type);
            recipe.Description = EditorGUILayout.TextField("配方简述:", recipe.Description);

            EditorGUILayout.Space(6);

            // ==================== 在场操作个体（非场所、不消耗，需在场） ====================
            DrawOperatorsSection(recipe);

            EditorGUILayout.Space(6);

            // ==================== 配方能力检定需求 ====================
            DrawAbilityChecksSection(recipe);

            EditorGUILayout.Space(8);

            // 根据配方模式绘制等式
            if (recipe.Type == RecipeType.Transformation)
            {
                DrawTransformationRecipeSection(recipe);
            }
            else
            {
                DrawSynthesisRecipeSection(recipe);
            }

            EditorGUILayout.Space(12);

            // 实时等式公式预览
            RecipeEditorStyles.DrawSectionHeader("配方等式实时预览");
            string formula = recipe.GetFormulaDisplay(
                id => _database.GetItemDisplayName(id),
                id => _database.GetTagDisplayName(id),
                id => _database.GetLocationDisplayName(id)
            );
            EditorGUILayout.TextArea(formula, RecipeEditorStyles.FormulaBox);
        }

        /// <summary>
        /// 绘制在场操作个体设置区（非场所，不消耗，代表需要某某在场操作，支持不限量）
        /// </summary>
        private void DrawOperatorsSection(RecipeDefinition recipe)
        {
            RecipeEditorStyles.DrawSectionHeader("在场操作人员（非场所·不消耗·无损耗）", "代表需要人员在场操作（如村民、工匠、角色），人员参与不消耗卡牌本体也不产生装备磨损");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            if (recipe.Operators == null) recipe.Operators = new List<RecipeOperatorRequirement>();

            if (recipe.Operators.Count == 0)
            {
                EditorGUILayout.HelpBox("当前无需任何人员在场操作（无人值守/自动合成）。", MessageType.None);
            }
            else
            {
                for (int i = 0; i < recipe.Operators.Count; i++)
                {
                    var op = recipe.Operators[i];
                    if (op == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"人员 {i + 1}:", GUILayout.Width(45));

                    op.MatchType = (IngredientMatchType)EditorGUILayout.EnumPopup(op.MatchType, GUILayout.Width(75));

                    if (op.MatchType == IngredientMatchType.ExactItem)
                    {
                        string[] itemOptions = GetItemDropdownOptions(out List<string> itemIds);
                        int curIdx = itemIds.IndexOf(op.TargetId ?? "");
                        int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), itemOptions, GUILayout.Width(140));
                        if (sel >= 0 && sel < itemIds.Count) op.TargetId = itemIds[sel];
                    }
                    else
                    {
                        string[] tagOptions = GetTagDropdownOptions(out List<string> tagIds);
                        int curIdx = tagIds.IndexOf(op.TargetId ?? "");
                        int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), tagOptions, GUILayout.Width(140));
                        if (sel >= 0 && sel < tagIds.Count) op.TargetId = tagIds[sel];
                    }

                    EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                    op.Quantity = EditorGUILayout.IntField(Math.Max(1, op.Quantity), GUILayout.Width(45));

                    EditorGUILayout.LabelField("分工角色:", GUILayout.Width(55));
                    op.Role = EditorGUILayout.TextField(op.Role, GUILayout.Width(80));
                    EditorGUILayout.LabelField("(不消耗·无损耗)", EditorStyles.miniLabel, GUILayout.Width(95));

                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        recipe.Operators.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("+ 添加在场操作人员", GUILayout.Width(150)))
            {
                recipe.Operators.Add(new RecipeOperatorRequirement(IngredientMatchType.ExactItem, "", 1, "操作者"));
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 配方能力检定需求配置（需要多少指定能力值才能成功或触发分支）
        /// </summary>
        private void DrawAbilityChecksSection(RecipeDefinition recipe)
        {
            RecipeEditorStyles.DrawSectionHeader("配方能力检定需求", "配置本配方所需达标的能力数值门槛，支持冶炼、烹饪、事件检定与分支判定");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            if (recipe.AbilityChecks == null) recipe.AbilityChecks = new List<RecipeAbilityCheck>();

            if (recipe.AbilityChecks.Count == 0)
            {
                EditorGUILayout.HelpBox("当前无能力检定要求（只要原料与工位满足即可100%成功）。", MessageType.None);
            }
            else
            {
                for (int i = 0; i < recipe.AbilityChecks.Count; i++)
                {
                    var chk = recipe.AbilityChecks[i];
                    if (chk == null) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"检定 {i + 1}:", GUILayout.Width(45));

                    // 能力选择
                    string[] abilityOptions = GetAbilityDropdownOptions(out List<string> abIds);
                    int curAbIdx = abIds.IndexOf(chk.AbilityId ?? "");
                    int chosenAb = EditorGUILayout.Popup(Math.Max(0, curAbIdx), abilityOptions, GUILayout.Width(140));
                    if (chosenAb >= 0 && chosenAb < abIds.Count)
                    {
                        chk.AbilityId = abIds[chosenAb];
                    }

                    // 比较符号
                    string[] opOptions = new string[] { ">=", "==", "<=", ">", "<" };
                    int opIdx = Array.IndexOf(opOptions, chk.Operator);
                    int chosenOp = EditorGUILayout.Popup(Math.Max(0, opIdx), opOptions, GUILayout.Width(50));
                    if (chosenOp >= 0) chk.Operator = opOptions[chosenOp];

                    EditorGUILayout.LabelField("门槛点数:", GUILayout.Width(60));
                    chk.RequiredValue = EditorGUILayout.FloatField(chk.RequiredValue, GUILayout.Width(50));

                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        recipe.AbilityChecks.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("未达标分支反馈:", GUILayout.Width(100));
                    chk.FailureBranchNote = EditorGUILayout.TextField(chk.FailureBranchNote);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                }
            }

            if (GUILayout.Button("+ 添加能力检定门槛", GUILayout.Width(150)))
            {
                string defaultAb = _database.Abilities != null && _database.Abilities.Count > 0 ? _database.Abilities[0].Id : "cooking";
                recipe.AbilityChecks.Add(new RecipeAbilityCheck(defaultAb, 10f, ">="));
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制标准合成配方（左右容量不限量）
        /// </summary>
        private void DrawSynthesisRecipeSection(RecipeDefinition recipe)
        {
            RecipeEditorStyles.DrawSectionHeader("等式左侧：原料与工具输入（不限容量）", "支持添加任意数量的材料；区分原料（完全消耗）与工具（非耗材·可损耗，默认暂不设损耗）");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            if (recipe.Inputs == null) recipe.Inputs = new List<RecipeInputSlot>();

            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                var slot = recipe.Inputs[i];
                if (slot == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"材料 {i + 1}:", GUILayout.Width(45));

                slot.MatchType = (IngredientMatchType)EditorGUILayout.EnumPopup(slot.MatchType, GUILayout.Width(75));

                if (slot.MatchType == IngredientMatchType.ExactItem)
                {
                    string[] itemOptions = GetItemDropdownOptions(out List<string> itemIds);
                    int curIdx = itemIds.IndexOf(slot.TargetId ?? "");
                    int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), itemOptions, GUILayout.Width(140));
                    if (sel >= 0 && sel < itemIds.Count) slot.TargetId = itemIds[sel];
                }
                else
                {
                    string[] tagOptions = GetTagDropdownOptions(out List<string> tagIds);
                    int curIdx = tagIds.IndexOf(slot.TargetId ?? "");
                    int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), tagOptions, GUILayout.Width(140));
                    if (sel >= 0 && sel < tagIds.Count) slot.TargetId = tagIds[sel];
                }

                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                slot.Quantity = EditorGUILayout.IntField(Math.Max(1, slot.Quantity), GUILayout.Width(45));

                int slotType = slot.Consume ? 0 : 1;
                int newSlotType = EditorGUILayout.Popup(slotType, new string[] { "原料 (完全消耗)", "工具 (非耗材·可损耗)" }, GUILayout.Width(135));
                slot.Consume = (newSlotType == 0);
                if (!slot.Consume)
                {
                    EditorGUILayout.LabelField("损耗:", GUILayout.Width(35));
                    slot.DurabilityCost = EditorGUILayout.IntField(Math.Max(0, slot.DurabilityCost), GUILayout.Width(35));
                }

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    recipe.Inputs.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 添加输入项 (原料/工具)", GUILayout.Width(150)))
            {
                recipe.Inputs.Add(new RecipeInputSlot(IngredientMatchType.ExactItem, "", 1, true));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 转换方向箭头
            EditorGUILayout.LabelField("══════════════════════════▶ 合成产出 ══════════════════════════", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(6);

            RecipeEditorStyles.DrawSectionHeader("等式右侧：合成产物（不限主产物与副产物数量）", "支持添加任意数量的主产物与副产物，支持概率产出与产物类别标记");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            if (recipe.Outputs == null) recipe.Outputs = new List<RecipeOutputSlot>();

            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                var slot = recipe.Outputs[i];
                if (slot == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"产出 {i + 1}:", GUILayout.Width(45));

                string[] itemOptions = GetItemDropdownOptions(out List<string> itemIds);
                int curIdx = itemIds.IndexOf(slot.ItemId ?? "");
                int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), itemOptions, GUILayout.Width(140));
                if (sel >= 0 && sel < itemIds.Count) slot.ItemId = itemIds[sel];

                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                slot.Quantity = EditorGUILayout.IntField(Math.Max(1, slot.Quantity), GUILayout.Width(45));

                EditorGUILayout.LabelField("几率%:", GUILayout.Width(40));
                float percent = EditorGUILayout.FloatField(slot.Chance * 100f, GUILayout.Width(40));
                slot.Chance = Mathf.Clamp01(percent / 100f);

                // 产物类别标记：★ 主产物 vs ⚑ 副产物（不限数量）
                int outTypeIdx = (slot.OutputType == RecipeOutputType.Byproduct || slot.IsByproduct) ? 1 : 0;
                int newOutType = EditorGUILayout.Popup(outTypeIdx, new string[] { "★ 主产物", "⚑ 副产物" }, GUILayout.Width(82));
                slot.OutputType = (newOutType == 1) ? RecipeOutputType.Byproduct : RecipeOutputType.Primary;
                slot.IsByproduct = (slot.OutputType == RecipeOutputType.Byproduct);

                // 检定触发分支（常规 / 检定达标 / 未达标失败）
                int trigIdx = (int)slot.TriggerCondition;
                int newTrig = EditorGUILayout.Popup(trigIdx, new string[] { "常规产出", "★检定达标产出", "⚠️未达标失败分支" }, GUILayout.Width(115));
                slot.TriggerCondition = (OutputTriggerCondition)newTrig;

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    recipe.Outputs.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 添加主产物 (不限数量)", GUILayout.Width(155)))
            {
                recipe.Outputs.Add(new RecipeOutputSlot("", 1, 1.0f, RecipeOutputType.Primary));
            }
            if (GUILayout.Button("+ 添加副产物 (不限数量)", GUILayout.Width(155)))
            {
                recipe.Outputs.Add(new RecipeOutputSlot("", 1, 1.0f, RecipeOutputType.Byproduct));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制自身转变配方（物品 A + 条件 ──> 物品 B + 副产物）
        /// </summary>
        private void DrawTransformationRecipeSection(RecipeDefinition recipe)
        {
            RecipeEditorStyles.DrawSectionHeader("自身转变主体（物品 A）", "指定发生质变/演变的主体物品");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("转变源物品 (物品 A):", GUILayout.Width(130));

            string[] itemOptions = GetItemDropdownOptions(out List<string> itemIds);
            int curSrcIdx = itemIds.IndexOf(recipe.SourceItemId ?? "");
            int selSrc = EditorGUILayout.Popup(Math.Max(0, curSrcIdx), itemOptions, GUILayout.Width(160));
            if (selSrc >= 0 && selSrc < itemIds.Count) recipe.SourceItemId = itemIds[selSrc];

            EditorGUILayout.LabelField("消耗数量:", GUILayout.Width(60));
            recipe.SourceQuantity = EditorGUILayout.IntField(Math.Max(1, recipe.SourceQuantity), GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 转变条件
            RecipeEditorStyles.DrawSectionHeader("转变附加条件列表（不限容量）", "例如所需工具、辅料消耗、在场操作人员、持续时间、环境属性等");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            if (recipe.TransformationConditions == null) recipe.TransformationConditions = new List<RecipeCondition>();

            if (recipe.TransformationConditions.Count == 0)
            {
                EditorGUILayout.HelpBox("当前无附加条件（仅在指定场所放置即可自然转变）。", MessageType.None);
            }
            else
            {
                for (int c = 0; c < recipe.TransformationConditions.Count; c++)
                {
                    var cond = recipe.TransformationConditions[c];
                    if (cond == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"条件 {c + 1}:", GUILayout.Width(45));
                    cond.Type = (ConditionType)EditorGUILayout.EnumPopup(cond.Type, GUILayout.Width(110));

                    switch (cond.Type)
                    {
                        case ConditionType.RequireTool:
                        case ConditionType.RequireMaterial:
                        case ConditionType.RequireOperator:
                            // 选物品或输入标签
                            int itemIdx = itemIds.IndexOf(cond.TargetId ?? "");
                            int selItem = EditorGUILayout.Popup(Math.Max(0, itemIdx), itemOptions, GUILayout.Width(130));
                            if (selItem >= 0 && selItem < itemIds.Count) cond.TargetId = itemIds[selItem];

                            if (cond.Type == ConditionType.RequireTool)
                            {
                                EditorGUILayout.LabelField("单次损耗:", GUILayout.Width(55));
                                cond.DurabilityCost = EditorGUILayout.IntField(Math.Max(0, cond.DurabilityCost), GUILayout.Width(35));
                            }
                            else if (cond.Type == ConditionType.RequireMaterial)
                            {
                                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                                cond.Value = EditorGUILayout.IntField(Math.Max(1, cond.Value), GUILayout.Width(45));
                            }
                            else if (cond.Type == ConditionType.RequireOperator)
                            {
                                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                                cond.Value = EditorGUILayout.IntField(Math.Max(1, cond.Value), GUILayout.Width(45));
                                EditorGUILayout.LabelField("(不消耗·无损耗)", EditorStyles.miniLabel, GUILayout.Width(95));
                            }
                            break;

                        case ConditionType.DurationMinutes:
                            EditorGUILayout.LabelField("等待时间(分):", GUILayout.Width(80));
                            cond.Value = EditorGUILayout.IntField(Math.Max(1, cond.Value), GUILayout.Width(60));
                            break;

                        case ConditionType.EnvironmentCondition:
                            EditorGUILayout.LabelField("环境要求:", GUILayout.Width(60));
                            cond.TargetId = EditorGUILayout.TextField(cond.TargetId, GUILayout.Width(120));
                            break;

                        case ConditionType.PropertyThreshold:
                            cond.TargetId = EditorGUILayout.TextField(cond.TargetId, GUILayout.Width(70));
                            cond.Operator = EditorGUILayout.TextField(cond.Operator, GUILayout.Width(30));
                            cond.Value = EditorGUILayout.IntField(cond.Value, GUILayout.Width(45));
                            break;
                    }

                    cond.Note = EditorGUILayout.TextField(cond.Note);

                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        recipe.TransformationConditions.RemoveAt(c);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("+ 添加转变条件", GUILayout.Width(130)))
            {
                recipe.TransformationConditions.Add(new RecipeCondition(ConditionType.RequireTool, "", 1));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 转变目标产物
            RecipeEditorStyles.DrawSectionHeader("转变目标结果（物品 B）与伴生副产物", "主体转变后的新物品，以及伴随生成的副产物");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("转变生成目标 (物品 B):", GUILayout.Width(130));

            int curDstIdx = itemIds.IndexOf(recipe.TargetItemId ?? "");
            int selDst = EditorGUILayout.Popup(Math.Max(0, curDstIdx), itemOptions, GUILayout.Width(160));
            if (selDst >= 0 && selDst < itemIds.Count) recipe.TargetItemId = itemIds[selDst];

            EditorGUILayout.LabelField("生成数量:", GUILayout.Width(60));
            recipe.TargetQuantity = EditorGUILayout.IntField(Math.Max(1, recipe.TargetQuantity), GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 伴生副产物
            EditorGUILayout.LabelField("伴生副产物列表 (例如炉渣、骨头、灰烬):", EditorStyles.miniBoldLabel);
            if (recipe.TransformationByproducts == null) recipe.TransformationByproducts = new List<RecipeOutputSlot>();

            for (int b = 0; b < recipe.TransformationByproducts.Count; b++)
            {
                var bp = recipe.TransformationByproducts[b];
                if (bp == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"副产物 {b + 1}:", GUILayout.Width(60));

                int curBpIdx = itemIds.IndexOf(bp.ItemId ?? "");
                int selBp = EditorGUILayout.Popup(Math.Max(0, curBpIdx), itemOptions, GUILayout.Width(140));
                if (selBp >= 0 && selBp < itemIds.Count) bp.ItemId = itemIds[selBp];

                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                bp.Quantity = EditorGUILayout.IntField(Math.Max(1, bp.Quantity), GUILayout.Width(45));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    recipe.TransformationByproducts.RemoveAt(b);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 添加伴生副产物", GUILayout.Width(130)))
            {
                recipe.TransformationByproducts.Add(new RecipeOutputSlot("", 1, 1f, true));
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 页签 5: 副产线管理（伴生修正、概率覆盖与产出拦截）

        private void DrawAuxiliaryLinesPage()
        {
            EditorGUILayout.BeginHorizontal();

            // 左侧：副产线列表
            EditorGUILayout.BeginVertical(GUILayout.Width(270));
            RecipeEditorStyles.DrawSectionHeader("副产线列表", "伴生增益、概率覆盖与防止产出规则");

            // 搜索栏
            _auxSearch = EditorGUILayout.TextField(_auxSearch, EditorStyles.toolbarSearchField);

            _leftListScroll = EditorGUILayout.BeginScrollView(_leftListScroll, RecipeEditorStyles.CardBox);
            if (_database.AuxiliaryLines != null)
            {
                for (int i = 0; i < _database.AuxiliaryLines.Count; i++)
                {
                    var line = _database.AuxiliaryLines[i];
                    if (line == null) continue;

                    if (!string.IsNullOrEmpty(_auxSearch))
                    {
                        bool match = (!string.IsNullOrEmpty(line.Name) && line.Name.IndexOf(_auxSearch, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                     (!string.IsNullOrEmpty(line.Id) && line.Id.IndexOf(_auxSearch, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!match) continue;
                    }

                    bool isSelected = (_selectedAuxIndex == i);
                    GUIStyle itemStyle = isSelected ? RecipeEditorStyles.ListItemSelected : RecipeEditorStyles.ListItemNormal;

                    EditorGUILayout.BeginHorizontal(itemStyle);
                    if (GUILayout.Button(line.Name ?? line.Id, EditorStyles.label))
                    {
                        _selectedAuxIndex = i;
                        GUI.FocusControl(null);
                    }

                    // 徽章提示：若包含拦截项则标红
                    bool hasPrevention = line.OutputModifiers != null && line.OutputModifiers.Exists(m => m != null && m.IsPrevention);
                    if (hasPrevention)
                    {
                        RecipeEditorStyles.DrawBadge("🛡️含拦截", new Color(0.8f, 0.2f, 0.2f));
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge("⚡增益", new Color(0.2f, 0.6f, 0.8f));
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 新建副产线"))
            {
                AddNewAuxiliaryLine();
            }
            if (_selectedAuxIndex >= 0 && _selectedAuxIndex < (_database.AuxiliaryLines?.Count ?? 0))
            {
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _database.AuxiliaryLines.RemoveAt(_selectedAuxIndex);
                    _selectedAuxIndex = -1;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 右侧：副产线详细配置
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);
            if (_selectedAuxIndex >= 0 && _selectedAuxIndex < (_database.AuxiliaryLines?.Count ?? 0))
            {
                DrawAuxiliaryLineDetail(_database.AuxiliaryLines[_selectedAuxIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox("请在左侧选择或新建一条副产线规则。\n副产线可跨配方生效：当任何配方满足指定标签/物品/工位，且投入额外媒介卡牌时，追加产物、覆盖概率或拦截产出。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();
        }

        private void AddNewAuxiliaryLine()
        {
            if (_database.AuxiliaryLines == null) _database.AuxiliaryLines = new List<RecipeAuxiliaryLine>();
            string newId = "aux_line_" + (_database.AuxiliaryLines.Count + 1);
            var newLine = new RecipeAuxiliaryLine(newId, "新副产线规则", "投入媒介卡牌触发的伴生产出或产物拦截规则");
            newLine.ExtraInputs.Add(new RecipeInputSlot(IngredientMatchType.ExactItem, "", 1, false, 0));
            newLine.OutputModifiers.Add(new RecipeOutputModifier("", 1, 1.0f, RecipeOutputType.Byproduct));
            _database.AuxiliaryLines.Add(newLine);
            _selectedAuxIndex = _database.AuxiliaryLines.Count - 1;
            _statusMessage = $"已新建副产线: {newLine.Name}";
        }

        private void DrawAuxiliaryLineDetail(RecipeAuxiliaryLine line)
        {
            RecipeEditorStyles.DrawSectionHeader("副产线基本信息", "跨配方挂接生效的伴生规则");
            line.Id = EditorGUILayout.TextField("副产线 ID (唯一):", line.Id);
            line.Name = EditorGUILayout.TextField("副产线名称:", line.Name);
            line.Description = EditorGUILayout.TextField("描述说明:", line.Description);

            EditorGUILayout.Space(8);

            // ==================== 1. 多维度复合匹配条件 ====================
            RecipeEditorStyles.DrawSectionHeader("1. 宿主配方复合匹配条件（多选）", "只要配方符合以下条件之一（或全部），投入媒介即可激活本副产线");
            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);

            line.MatchCombineMode = (AuxiliaryMatchCombineMode)EditorGUILayout.EnumPopup("条件组合方式:", line.MatchCombineMode);

            // A. 匹配标签列表 (多选)
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("匹配标签列表 (配方输入包含这些标签即命中):", EditorStyles.boldLabel);
            if (line.MatchTagIds == null) line.MatchTagIds = new List<string>();
            for (int t = 0; t < line.MatchTagIds.Count; t++)
            {
                EditorGUILayout.BeginHorizontal();
                string curTagId = line.MatchTagIds[t];
                EditorGUILayout.LabelField($"🏷️ #{_database.GetTagDisplayName(curTagId)} ({curTagId})", GUILayout.Width(220));
                if (GUILayout.Button("移除", GUILayout.Width(50)))
                {
                    line.MatchTagIds.RemoveAt(t);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 添加匹配标签下拉
            string[] tagOptions = GetTagDropdownOptions(out List<string> allTagIds);
            int addTagSel = EditorGUILayout.Popup(0, InsertOption(tagOptions, "+ 添加匹配标签..."), GUILayout.Width(180));
            if (addTagSel > 0 && addTagSel <= allTagIds.Count)
            {
                string chosenTag = allTagIds[addTagSel - 1];
                if (!line.MatchTagIds.Contains(chosenTag)) line.MatchTagIds.Add(chosenTag);
            }

            EditorGUILayout.Space(4);

            // B. 匹配物品列表 (多选)
            EditorGUILayout.LabelField("匹配特定物品 (配方输入包含这些具体物品即命中):", EditorStyles.boldLabel);
            if (line.MatchItemIds == null) line.MatchItemIds = new List<string>();
            for (int it = 0; it < line.MatchItemIds.Count; it++)
            {
                EditorGUILayout.BeginHorizontal();
                string curItemId = line.MatchItemIds[it];
                EditorGUILayout.LabelField($"📦 {_database.GetItemDisplayName(curItemId)} ({curItemId})", GUILayout.Width(220));
                if (GUILayout.Button("移除", GUILayout.Width(50)))
                {
                    line.MatchItemIds.RemoveAt(it);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            string[] itemOptions = GetItemDropdownOptions(out List<string> allItemIds);
            int addItemSel = EditorGUILayout.Popup(0, InsertOption(itemOptions, "+ 添加匹配物品..."), GUILayout.Width(180));
            if (addItemSel > 0 && addItemSel <= allItemIds.Count)
            {
                string chosenItem = allItemIds[addItemSel - 1];
                if (!line.MatchItemIds.Contains(chosenItem)) line.MatchItemIds.Add(chosenItem);
            }

            EditorGUILayout.Space(4);

            // C. 匹配工位场所列表 (多选)
            EditorGUILayout.LabelField("匹配发生工位 (在这些特定工位场所制作时即命中):", EditorStyles.boldLabel);
            if (line.MatchLocationIds == null) line.MatchLocationIds = new List<string>();
            for (int l = 0; l < line.MatchLocationIds.Count; l++)
            {
                EditorGUILayout.BeginHorizontal();
                string curLocId = line.MatchLocationIds[l];
                EditorGUILayout.LabelField($"🏛️ {_database.GetLocationDisplayName(curLocId)} ({curLocId})", GUILayout.Width(220));
                if (GUILayout.Button("移除", GUILayout.Width(50)))
                {
                    line.MatchLocationIds.RemoveAt(l);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            string[] locOptions = GetLocationDropdownOptions(out List<string> allLocIds);
            int addLocSel = EditorGUILayout.Popup(0, InsertOption(locOptions, "+ 添加匹配工位..."), GUILayout.Width(180));
            if (addLocSel > 0 && addLocSel <= allLocIds.Count)
            {
                string chosenLoc = allLocIds[addLocSel - 1];
                if (!line.MatchLocationIds.Contains(chosenLoc)) line.MatchLocationIds.Add(chosenLoc);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ==================== 2. 额外投入媒介卡牌 ====================
            RecipeEditorStyles.DrawSectionHeader("2. 额外投入媒介（激活副产线所需的卡牌）", "如手链、厚皮手套、熏香，支持设置是否消耗或耐久磨损");
            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            if (line.ExtraInputs == null) line.ExtraInputs = new List<RecipeInputSlot>();

            for (int i = 0; i < line.ExtraInputs.Count; i++)
            {
                var inp = line.ExtraInputs[i];
                if (inp == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"投入 {i + 1}:", GUILayout.Width(45));

                inp.MatchType = (IngredientMatchType)EditorGUILayout.EnumPopup(inp.MatchType, GUILayout.Width(75));
                if (inp.MatchType == IngredientMatchType.ExactItem)
                {
                    int curIdx = allItemIds.IndexOf(inp.TargetId ?? "");
                    int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), itemOptions, GUILayout.Width(130));
                    if (sel >= 0 && sel < allItemIds.Count) inp.TargetId = allItemIds[sel];
                }
                else
                {
                    int curIdx = allTagIds.IndexOf(inp.TargetId ?? "");
                    int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), tagOptions, GUILayout.Width(130));
                    if (sel >= 0 && sel < allTagIds.Count) inp.TargetId = allTagIds[sel];
                }

                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                inp.Quantity = EditorGUILayout.IntField(Math.Max(1, inp.Quantity), GUILayout.Width(40));

                int slotType = inp.Consume ? 0 : 1;
                int newSlotType = EditorGUILayout.Popup(slotType, new string[] { "消耗原料", "非耗材器具" }, GUILayout.Width(85));
                inp.Consume = (newSlotType == 0);
                if (!inp.Consume)
                {
                    EditorGUILayout.LabelField("损耗:", GUILayout.Width(35));
                    inp.DurabilityCost = EditorGUILayout.IntField(Math.Max(0, inp.DurabilityCost), GUILayout.Width(35));
                }

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    line.ExtraInputs.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 添加额外投入卡牌", GUILayout.Width(140)))
            {
                line.ExtraInputs.Add(new RecipeInputSlot(IngredientMatchType.ExactItem, "", 1, false, 0));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ==================== 3. 产物修饰与概率覆盖 ====================
            RecipeEditorStyles.DrawSectionHeader("3. 产物修饰与覆盖（防止产出 / 概率覆盖 / 额外追加）", "0% 表示彻底防止/拦截产出；100% 覆盖为必出；新物品则作为额外产物追加");
            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);

            line.SuppressBaseOutputs = EditorGUILayout.ToggleLeft("🚫 彻底阻断宿主配方原有全部产出 (只保留本副产线的产物)", line.SuppressBaseOutputs);

            EditorGUILayout.Space(4);

            if (line.OutputModifiers == null) line.OutputModifiers = new List<RecipeOutputModifier>();

            for (int m = 0; m < line.OutputModifiers.Count; m++)
            {
                var mod = line.OutputModifiers[m];
                if (mod == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"修饰 {m + 1}:", GUILayout.Width(45));

                int curIdx = allItemIds.IndexOf(mod.ItemId ?? "");
                int sel = EditorGUILayout.Popup(Math.Max(0, curIdx), itemOptions, GUILayout.Width(130));
                if (sel >= 0 && sel < allItemIds.Count) mod.ItemId = allItemIds[sel];

                EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
                mod.Quantity = EditorGUILayout.IntField(Math.Max(1, mod.Quantity), GUILayout.Width(40));

                EditorGUILayout.LabelField("几率%:", GUILayout.Width(40));
                float percent = EditorGUILayout.FloatField(mod.Chance * 100f, GUILayout.Width(40));
                mod.Chance = Mathf.Clamp01(percent / 100f);

                // 类别标记
                mod.OutputType = (RecipeOutputType)EditorGUILayout.EnumPopup(mod.OutputType, GUILayout.Width(75));

                // 状态徽章
                if (mod.IsPrevention)
                {
                    RecipeEditorStyles.DrawBadge("🛡️拦截防止(0%)", new Color(0.85f, 0.2f, 0.2f));
                }
                else if (mod.Chance >= 0.999f)
                {
                    RecipeEditorStyles.DrawBadge("★必定产出(100%)", new Color(0.2f, 0.7f, 0.3f));
                }
                else
                {
                    RecipeEditorStyles.DrawBadge($"⚑{Math.Round(mod.Chance * 100)}%", new Color(0.2f, 0.5f, 0.8f));
                }

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    line.OutputModifiers.RemoveAt(m);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 添加产出追加项 (100% 必出)", GUILayout.Width(170)))
            {
                line.OutputModifiers.Add(new RecipeOutputModifier("", 1, 1.0f, RecipeOutputType.Byproduct));
            }
            if (GUILayout.Button("+ 添加拦截防止项 (0% 彻底阻止)", GUILayout.Width(180)))
            {
                line.OutputModifiers.Add(new RecipeOutputModifier("", 1, 0.0f, RecipeOutputType.Byproduct, "拦截防止该物品产出"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // ==================== 4. 实时逻辑等式摘要 ====================
            RecipeEditorStyles.DrawSectionHeader("副产线生效逻辑实时预览");
            string formula = line.GetSummaryDisplay(
                id => _database.GetItemDisplayName(id),
                id => _database.GetTagDisplayName(id),
                id => _database.GetLocationDisplayName(id)
            );
            EditorGUILayout.TextArea(formula, RecipeEditorStyles.FormulaBox);
        }

        private string[] InsertOption(string[] options, string firstOption)
        {
            var list = new List<string> { firstOption };
            if (options != null) list.AddRange(options);
            return list.ToArray();
        }

        #endregion

        #region 页签 6: 概览与校验 (Overview & Check)

        private void DrawOverviewPage()
        {
            _rightDetailScroll = EditorGUILayout.BeginScrollView(_rightDetailScroll);

            RecipeEditorStyles.DrawSectionHeader("数据库基本信息与统计");

            EditorGUILayout.BeginVertical(RecipeEditorStyles.CardBox);
            _database.DatabaseName = EditorGUILayout.TextField("数据库名称:", _database.DatabaseName);
            _database.ContentVersion = EditorGUILayout.TextField("版本号:", _database.ContentVersion);
            _database.Description = EditorGUILayout.TextField("说明描述:", _database.Description);

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"• 物品总数: {_database?.Items?.Count ?? 0}", GUILayout.Width(140));
            EditorGUILayout.LabelField($"• 标签总数: {_database?.Tags?.Count ?? 0}", GUILayout.Width(140));
            EditorGUILayout.LabelField($"• 能力总数: {_database?.Abilities?.Count ?? 0}", GUILayout.Width(140));
            EditorGUILayout.LabelField($"• 合成场所: {_database?.Locations?.Count ?? 0}", GUILayout.Width(140));
            EditorGUILayout.LabelField($"• 配方总数: {_database?.Recipes?.Count ?? 0}", GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(12);

            RecipeEditorStyles.DrawSectionHeader("完整性静态校验结果");

            if (GUILayout.Button("重新执行数据完整性校验", GUILayout.Height(30)))
            {
                RunValidation();
            }

            EditorGUILayout.Space(6);

            if (_lastValidationResults == null || _lastValidationResults.Count == 0)
            {
                EditorGUILayout.HelpBox("✔ 校验通过！未发现任何悬空引用、ID 冲突或逻辑问题。", MessageType.Info);
            }
            else
            {
                int errors = 0;
                int warnings = 0;
                foreach (var msg in _lastValidationResults)
                {
                    if (msg.Severity == ValidationSeverity.Error) errors++;
                    else if (msg.Severity == ValidationSeverity.Warning) warnings++;
                }

                EditorGUILayout.LabelField($"发现问题: {errors} 个错误, {warnings} 个警告, {_lastValidationResults.Count - errors - warnings} 个优化提示", EditorStyles.boldLabel);

                foreach (var msg in _lastValidationResults)
                {
                    MessageType mType = MessageType.Info;
                    if (msg.Severity == ValidationSeverity.Error) mType = MessageType.Error;
                    else if (msg.Severity == ValidationSeverity.Warning) mType = MessageType.Warning;

                    EditorGUILayout.BeginHorizontal(RecipeEditorStyles.CardBox);
                    EditorGUILayout.HelpBox(msg.ToString(), mType);

                    if (GUILayout.Button("定位", GUILayout.Width(50), GUILayout.Height(35)))
                    {
                        NavigateToValidationSource(msg);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void NavigateToValidationSource(ValidationMessage msg)
        {
            if (msg.SourceType == "Item")
            {
                _selectedTab = 0;
                var item = _database.FindItem(msg.SourceId);
                if (item != null) _selectedItemIndex = _database.Items.IndexOf(item);
            }
            else if (msg.SourceType == "Tag")
            {
                _selectedTab = 1;
                var tag = _database.FindTag(msg.SourceId);
                if (tag != null) _selectedTagIndex = _database.Tags.IndexOf(tag);
            }
            else if (msg.SourceType == "Ability")
            {
                _selectedTab = 2;
                var ab = _database.FindAbility(msg.SourceId);
                if (ab != null) _selectedAbilityIndex = _database.Abilities.IndexOf(ab);
            }
            else if (msg.SourceType == "Location")
            {
                _selectedTab = 3;
                var loc = _database.FindLocation(msg.SourceId);
                if (loc != null) _selectedLocationIndex = _database.Locations.IndexOf(loc);
            }
            else if (msg.SourceType == "Recipe")
            {
                _selectedTab = 4;
                var recipe = _database.FindRecipe(msg.SourceId);
                if (recipe != null) _selectedRecipeIndex = _database.Recipes.IndexOf(recipe);
            }
            else if (msg.SourceType == "AuxiliaryLine")
            {
                _selectedTab = 5;
                var line = _database.FindAuxiliaryLine(msg.SourceId);
                if (line != null) _selectedAuxIndex = _database.AuxiliaryLines.IndexOf(line);
            }
        }

        #endregion

        #region 拆包游戏参考展台（独立展示与借用）

        private void EnsureReferenceDatabasesLoaded()
        {
            if (_refDbTropical == null)
            {
                _refDbTropical = LoadReferenceDb("Assets/RecipeEditor/Data/热带岛屿配方参考.json");
            }
            if (_refDbFantasy == null)
            {
                _refDbFantasy = LoadReferenceDb("Assets/RecipeEditor/Data/奇幻森林配方参考.json");
            }
            if (_refDbFreeze == null)
            {
                _refDbFreeze = LoadReferenceDb("Assets/RecipeEditor/Data/别冻僵配方参考.json");
            }
        }

        private RecipeDatabaseData LoadReferenceDb(string assetPath)
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
            {
                return RecipeJsonSerializer.FromJson(textAsset.text);
            }
            string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            if (File.Exists(fullPath))
            {
                return RecipeJsonSerializer.ImportFromFile(fullPath, out _);
            }
            return new RecipeDatabaseData();
        }

        private string[] GetRefTabLabels()
        {
            EnsureReferenceDatabasesLoaded();
            return new string[]
            {
                $"🏝️ 热带岛屿参考 ({_refDbTropical?.Recipes?.Count ?? 0})",
                $"🌲 奇幻森林参考 ({_refDbFantasy?.Recipes?.Count ?? 0})",
                $"❄️ 别冻僵参考 ({_refDbFreeze?.Recipes?.Count ?? 0})"
            };
        }

        private RecipeDatabaseData GetReferenceDatabase(string gameKey, string assetPath)
        {
            EnsureReferenceDatabasesLoaded();
            if (gameKey == "tropical") return _refDbTropical;
            if (gameKey == "fantasy") return _refDbFantasy;
            if (gameKey == "freeze") return _refDbFreeze;
            return null;
        }

        private void DrawReferenceGamePage(string gameTitle, string gameKey, string assetPath)
        {
            var refDb = GetReferenceDatabase(gameKey, assetPath);
            if (refDb == null || refDb.Recipes == null)
            {
                EditorGUILayout.HelpBox($"未找到参考数据: {assetPath}，请确认数据文件存在。", MessageType.Warning);
                return;
            }

            // 顶部展台横幅
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{gameTitle} 真实配方参考展台</b>", RecipeEditorStyles.HeaderLabel);
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.25f, 0.7f, 0.35f);
            if (GUILayout.Button($"★ 一键批量引入该游戏全部配方 ({refDb.Recipes.Count}) 到当前库", GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("确认批量引入", $"确定将《{gameTitle}》的 {refDb.Recipes.Count} 个配方及关联物品、标签、场所批量合并到当前正在编辑的配方库中吗？\n（将使用智能增量合并，跳过已存在的相同 ID）", "确定引入", "取消"))
                {
                    var report = RecipeDataMerger.Merge(_database, refDb, MergeConflictPolicy.Skip);
                    _statusMessage = $"批量引入完成: {report}";
                    _selectedTab = 3;
                    _selectedRecipeIndex = _database.Recipes.Count > 0 ? 0 : -1;
                    RunValidation();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("重新载入", GUILayout.Width(70), GUILayout.Height(22)))
            {
                if (gameKey == "tropical") _refDbTropical = LoadReferenceDb(assetPath);
                else if (gameKey == "fantasy") _refDbFantasy = LoadReferenceDb(assetPath);
                else if (gameKey == "freeze") _refDbFreeze = LoadReferenceDb(assetPath);
                _statusMessage = $"已重新载入《{gameTitle}》参考数据";
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(refDb.Description))
            {
                EditorGUILayout.LabelField(refDb.Description, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.LabelField($"收录统计: 配方 {refDb.Recipes.Count} 项 | 物品 {refDb.Items.Count} 种 | 场所 {refDb.Locations.Count} 处 | 标签 {refDb.Tags.Count} 个", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(2);

            // 搜索与类型筛选工具栏
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索筛选:", GUILayout.Width(65));
            _refSearchQuery = EditorGUILayout.TextField(_refSearchQuery, EditorStyles.toolbarSearchField);
            if (!string.IsNullOrEmpty(_refSearchQuery))
            {
                if (GUILayout.Button("清除", EditorStyles.miniButton, GUILayout.Width(45)))
                {
                    _refSearchQuery = "";
                    GUI.FocusControl(null);
                }
            }

            string[] typeFilters = new string[] { "全部类型", "标准合成", "自身转变" };
            _refRecipeTypeFilter = EditorGUILayout.Popup(_refRecipeTypeFilter, typeFilters, GUILayout.Width(85));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // 左右分栏
            EditorGUILayout.BeginHorizontal();

            // 筛选出符合条件的配方
            var filteredRecipes = new List<RecipeDefinition>();
            for (int i = 0; i < refDb.Recipes.Count; i++)
            {
                var r = refDb.Recipes[i];
                if (r == null) continue;
                if (_refRecipeTypeFilter == 1 && r.Type != RecipeType.Synthesis) continue;
                if (_refRecipeTypeFilter == 2 && r.Type != RecipeType.Transformation) continue;

                if (!string.IsNullOrEmpty(_refSearchQuery))
                {
                    string q = _refSearchQuery.ToLowerInvariant();
                    bool match = (r.Name != null && r.Name.ToLowerInvariant().Contains(q)) ||
                                 (r.Id != null && r.Id.ToLowerInvariant().Contains(q)) ||
                                 (r.Description != null && r.Description.ToLowerInvariant().Contains(q));
                    if (!match) continue;
                }
                filteredRecipes.Add(r);
            }

            // 左侧配方列表
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            _refLeftScroll = EditorGUILayout.BeginScrollView(_refLeftScroll, EditorStyles.helpBox);

            if (filteredRecipes.Count == 0)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("未找到匹配的参考配方", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < filteredRecipes.Count; i++)
                {
                    var r = filteredRecipes[i];
                    bool isSelected = (_refSelectedRecipeId == r.Id);

                    EditorGUILayout.BeginVertical(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(r.Name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (r.Type == RecipeType.Transformation)
                    {
                        RecipeEditorStyles.DrawBadge("自身转变", new Color(0.9f, 0.5f, 0.2f));
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge("标准合成", new Color(0.3f, 0.7f, 0.4f));
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(r.Id, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    var loc = refDb.FindLocation(r.LocationId);
                    string locName = loc != null ? loc.Name : "随身/通用";
                    EditorGUILayout.LabelField($"📍 {locName}", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    string formulaText = r.GetFormulaDisplay(
                        id => refDb.GetItemDisplayName(id),
                        id => refDb.GetTagDisplayName(id),
                        id => refDb.GetLocationDisplayName(id)
                    );
                    EditorGUILayout.LabelField(formulaText, EditorStyles.wordWrappedMiniLabel);

                    if (GUILayout.Button(isSelected ? "已选中" : "查看详情", EditorStyles.miniButton))
                    {
                        _refSelectedRecipeId = r.Id;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 右侧详细信息与借用操作
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _refRightScroll = EditorGUILayout.BeginScrollView(_refRightScroll);

            RecipeDefinition selRecipe = null;
            if (!string.IsNullOrEmpty(_refSelectedRecipeId))
            {
                selRecipe = refDb.FindRecipe(_refSelectedRecipeId);
            }
            if (selRecipe == null && filteredRecipes.Count > 0)
            {
                selRecipe = filteredRecipes[0];
                _refSelectedRecipeId = selRecipe.Id;
            }

            if (selRecipe != null)
            {
                DrawReferenceRecipeDetails(selRecipe, refDb);
            }
            else
            {
                EditorGUILayout.Space(40);
                EditorGUILayout.LabelField("👈 请在左侧列表中点击选择一项参考配方", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField("支持查阅官方机制、等式组成，并可一键借用到当前开发项目库中", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawReferenceRecipeDetails(RecipeDefinition recipe, RecipeDatabaseData sourceDb)
        {
            if (recipe == null || sourceDb == null) return;

            // 顶部操作卡片
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"<b>{recipe.Name}</b> ({recipe.Id})", RecipeEditorStyles.HeaderLabel);
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.15f, 0.6f, 0.95f);
            if (GUILayout.Button("★ 借用/复制到我的配方库 (含关联项)", GUILayout.Height(24)))
            {
                CopyReferenceRecipeToActiveDatabase(recipe, sourceDb);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 配方等式实时预览
            RecipeEditorStyles.DrawSectionHeader("工艺等式预览");
            string formula = recipe.GetFormulaDisplay(
                id => sourceDb.GetItemDisplayName(id),
                id => sourceDb.GetTagDisplayName(id),
                id => sourceDb.GetLocationDisplayName(id)
            );
            EditorGUILayout.TextArea(formula, RecipeEditorStyles.FormulaBox);

            EditorGUILayout.Space(6);

            // 基础信息
            RecipeEditorStyles.DrawSectionHeader("基础信息");
            EditorGUILayout.LabelField("配方类型:", recipe.Type == RecipeType.Transformation ? "自身转变 (物品A + 条件 -> 目标物品B + 副产物)" : "标准合成 (多原料 -> 多产物)");
            EditorGUILayout.LabelField("制作耗时:", $"{recipe.DurationMinutes} 分钟");
            if (!string.IsNullOrEmpty(recipe.Description))
            {
                EditorGUILayout.LabelField("描述说明:", recipe.Description, EditorStyles.wordWrappedLabel);
            }

            // 合成场所
            EditorGUILayout.Space(4);
            RecipeEditorStyles.DrawSectionHeader("合成场所 / 工位要求");
            var loc = sourceDb.FindLocation(recipe.LocationId);
            if (loc != null)
            {
                EditorGUILayout.LabelField("场所名称:", $"{loc.Name} ({loc.Id})");
                EditorGUILayout.LabelField("工位形式:", loc.SourceType == LocationSourceType.Item ? $"[实体物品工位] 需使用物品: {sourceDb.GetItemDisplayName(loc.BoundItemId)} [工位设施·可损耗·暂不设]" : "[独立工位设施] [可损耗·暂不设]");
                if (!string.IsNullOrEmpty(loc.Description))
                {
                    EditorGUILayout.LabelField("场所说明:", loc.Description);
                }
            }
            else
            {
                EditorGUILayout.LabelField("工位要求:", "随身制作 / 无特定场所限制");
            }

            // 在场操作个体要求
            if (recipe.Operators != null && recipe.Operators.Count > 0)
            {
                EditorGUILayout.Space(4);
                RecipeEditorStyles.DrawSectionHeader($"在场操作人员要求 ({recipe.Operators.Count} 项·不消耗·无损耗)");
                foreach (var op in recipe.Operators)
                {
                    string opName = op.MatchType == IngredientMatchType.ExactItem ? sourceDb.GetItemDisplayName(op.TargetId) : sourceDb.GetTagDisplayName(op.TargetId);
                    string matchDesc = op.MatchType == IngredientMatchType.ExactItem ? "具体个体" : "标签匹配";
                    EditorGUILayout.LabelField($"👤 [{matchDesc}] {opName} x{op.Quantity} {(!string.IsNullOrEmpty(op.Role) ? $"({op.Role})" : "")} [在场人员·不消耗·无损耗]");
                }
            }

            // 输入原料清单
            EditorGUILayout.Space(4);
            RecipeEditorStyles.DrawSectionHeader($"输入原料 (等式左边: {recipe.Inputs?.Count ?? 0} 项)");
            if (recipe.Inputs != null && recipe.Inputs.Count > 0)
            {
                foreach (var inp in recipe.Inputs)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    if (inp.MatchType == IngredientMatchType.ExactItem)
                    {
                        string itemName = sourceDb.GetItemDisplayName(inp.TargetId);
                        GUILayout.Label($"📦 具体物品: <b>{itemName}</b> ({inp.TargetId})", EditorStyles.label);
                    }
                    else
                    {
                        string tagName = sourceDb.GetTagDisplayName(inp.TargetId);
                        GUILayout.Label($"🏷️ 标签匹配: <b>#{tagName}</b> ({inp.TargetId})", EditorStyles.label);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"数量: x{inp.Quantity}", EditorStyles.boldLabel);
                    if (!inp.Consume)
                    {
                        string wearTag = inp.DurabilityCost > 0 ? $"损耗:{inp.DurabilityCost}" : "可损耗·暂不设";
                        RecipeEditorStyles.DrawBadge($"工具 ({wearTag})", new Color(0.2f, 0.6f, 0.8f));
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge("原料 (完全消耗)", new Color(0.8f, 0.4f, 0.3f));
                    }
                    EditorGUILayout.EndHorizontal();

                    if (!string.IsNullOrEmpty(inp.Note))
                    {
                        EditorGUILayout.LabelField($"说明: {inp.Note}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.LabelField("（无原料输入要求）", EditorStyles.miniLabel);
            }

            // 输出产物清单
            EditorGUILayout.Space(4);
            RecipeEditorStyles.DrawSectionHeader($"输出产物 (等式右边: {recipe.Outputs?.Count ?? 0} 项)");
            if (recipe.Type == RecipeType.Transformation && !string.IsNullOrEmpty(recipe.TargetItemId))
            {
                string targetName = sourceDb.GetItemDisplayName(recipe.TargetItemId);
                EditorGUILayout.HelpBox($"自身转变目标: 核心原物转变为 [{targetName}]", MessageType.Info);
            }

            if (recipe.Outputs != null && recipe.Outputs.Count > 0)
            {
                foreach (var outp in recipe.Outputs)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    string itemName = sourceDb.GetItemDisplayName(outp.ItemId);
                    GUILayout.Label($"🎁 产物: <b>{itemName}</b> ({outp.ItemId})", EditorStyles.label);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"数量: x{outp.Quantity}", EditorStyles.boldLabel);
                    if (outp.Chance < 0.999f)
                    {
                        RecipeEditorStyles.DrawBadge($"概率: {Mathf.RoundToInt(outp.Chance * 100)}%", new Color(0.9f, 0.6f, 0.2f));
                    }
                    bool isBp = outp.OutputType == RecipeOutputType.Byproduct || outp.IsByproduct;
                    if (isBp)
                    {
                        RecipeEditorStyles.DrawBadge("⚑ 副产物", new Color(0.6f, 0.4f, 0.8f));
                    }
                    else
                    {
                        RecipeEditorStyles.DrawBadge("★ 主产物", new Color(0.2f, 0.7f, 0.3f));
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void CopyReferenceRecipeToActiveDatabase(RecipeDefinition refRecipe, RecipeDatabaseData sourceDb)
        {
            if (refRecipe == null || sourceDb == null || _database == null) return;

            int newItemsCount = 0;
            int newTagsCount = 0;
            int newLocationsCount = 0;

            // 1. 复制场所
            if (!string.IsNullOrEmpty(refRecipe.LocationId))
            {
                var targetLoc = _database.FindLocation(refRecipe.LocationId);
                if (targetLoc == null)
                {
                    var sourceLoc = sourceDb.FindLocation(refRecipe.LocationId);
                    if (sourceLoc != null)
                    {
                        _database.Locations.Add(sourceLoc.Clone());
                        newLocationsCount++;
                        if (sourceLoc.SourceType == LocationSourceType.Item && !string.IsNullOrEmpty(sourceLoc.BoundItemId))
                        {
                            CopyItemAndDependencies(sourceLoc.BoundItemId, sourceDb, ref newItemsCount, ref newTagsCount);
                        }
                    }
                }
            }

            // 2. 复制在场操作个体所需物品
            if (refRecipe.Operators != null)
            {
                foreach (var op in refRecipe.Operators)
                {
                    if (op.MatchType == IngredientMatchType.ExactItem)
                    {
                        CopyItemAndDependencies(op.TargetId, sourceDb, ref newItemsCount, ref newTagsCount);
                    }
                    else if (op.MatchType == IngredientMatchType.ByTag)
                    {
                        CopyTagIfNeeded(op.TargetId, sourceDb, ref newTagsCount);
                    }
                }
            }

            // 3. 复制输入项物品与标签
            if (refRecipe.Inputs != null)
            {
                foreach (var inp in refRecipe.Inputs)
                {
                    if (inp.MatchType == IngredientMatchType.ExactItem)
                    {
                        CopyItemAndDependencies(inp.TargetId, sourceDb, ref newItemsCount, ref newTagsCount);
                    }
                    else if (inp.MatchType == IngredientMatchType.ByTag)
                    {
                        CopyTagIfNeeded(inp.TargetId, sourceDb, ref newTagsCount);
                    }
                }
            }

            // 4. 复制自身转变目标物品与源物品
            if (refRecipe.Type == RecipeType.Transformation)
            {
                if (!string.IsNullOrEmpty(refRecipe.SourceItemId))
                {
                    CopyItemAndDependencies(refRecipe.SourceItemId, sourceDb, ref newItemsCount, ref newTagsCount);
                }
                if (!string.IsNullOrEmpty(refRecipe.TargetItemId))
                {
                    CopyItemAndDependencies(refRecipe.TargetItemId, sourceDb, ref newItemsCount, ref newTagsCount);
                }
            }

            // 5. 复制输出项物品
            if (refRecipe.Outputs != null)
            {
                foreach (var outp in refRecipe.Outputs)
                {
                    CopyItemAndDependencies(outp.ItemId, sourceDb, ref newItemsCount, ref newTagsCount);
                }
            }

            // 6. 复制配方本身（若 ID 重复则自动编号避免冲突）
            var newRecipe = refRecipe.Clone();
            string baseId = newRecipe.Id;
            string candidateId = baseId;
            int counter = 1;
            while (_database.FindRecipe(candidateId) != null)
            {
                candidateId = $"{baseId}_copy{counter++}";
            }
            newRecipe.Id = candidateId;
            _database.Recipes.Add(newRecipe);

            // 切换到配方管理页并聚焦新配方
            _selectedTab = 3;
            _selectedRecipeIndex = _database.Recipes.Count - 1;
            _statusMessage = $"已将配方 [{newRecipe.Name}] 借用至当前库！关联引入: 物品+{newItemsCount}, 标签+{newTagsCount}, 场所+{newLocationsCount}";
            RunValidation();
        }

        private void CopyItemAndDependencies(string itemId, RecipeDatabaseData sourceDb, ref int newItemsCount, ref int newTagsCount)
        {
            if (string.IsNullOrEmpty(itemId) || _database == null || sourceDb == null) return;
            if (_database.FindItem(itemId) != null) return;

            var sourceItem = sourceDb.FindItem(itemId);
            if (sourceItem != null)
            {
                _database.Items.Add(sourceItem.Clone());
                newItemsCount++;
                if (sourceItem.TagIds != null)
                {
                    foreach (var tagId in sourceItem.TagIds)
                    {
                        CopyTagIfNeeded(tagId, sourceDb, ref newTagsCount);
                    }
                }
            }
        }

        private void CopyTagIfNeeded(string tagId, RecipeDatabaseData sourceDb, ref int newTagsCount)
        {
            if (string.IsNullOrEmpty(tagId) || _database == null || sourceDb == null) return;
            if (_database.FindTag(tagId) != null) return;

            var sourceTag = sourceDb.FindTag(tagId);
            if (sourceTag != null)
            {
                _database.Tags.Add(sourceTag.Clone());
                newTagsCount++;
            }
        }

        #endregion

        #region 底部状态栏与辅助下拉选项生成

        private void DrawFooterStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"状态: {_statusMessage}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (_activeAsset != null)
            {
                EditorGUILayout.LabelField($"绑定资产: {_activeAsset.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("(未保存至资产)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        private string[] GetItemDropdownOptions(out List<string> itemIds)
        {
            itemIds = new List<string> { "" };
            var list = new List<string> { "-- 选择物品 --" };

            if (_database?.Items != null)
            {
                foreach (var it in _database.Items)
                {
                    if (it == null) continue;
                    itemIds.Add(it.Id);
                    list.Add($"{it.Name} ({it.Id})");
                }
            }
            return list.ToArray();
        }

        private string[] GetTagDropdownOptions(out List<string> tagIds)
        {
            tagIds = new List<string> { "" };
            var list = new List<string> { "-- 选择标签 --" };

            if (_database?.Tags != null)
            {
                foreach (var tg in _database.Tags)
                {
                    if (tg == null) continue;
                    tagIds.Add(tg.Id);
                    list.Add($"#{tg.Name} ({tg.Id})");
                }
            }
            return list.ToArray();
        }

        private string[] GetLocationDropdownOptions(out List<string> locationIds)
        {
            locationIds = new List<string> { "" };
            var list = new List<string> { "随身 / 徒手 (无特定场所限制)" };

            if (_database?.Locations != null)
            {
                foreach (var loc in _database.Locations)
                {
                    if (loc == null) continue;
                    locationIds.Add(loc.Id);
                    string typeMark = loc.SourceType == LocationSourceType.Item ? "[物品场所] " : "";
                    list.Add($"{typeMark}{loc.Name} ({loc.Id})");
                }
            }
            return list.ToArray();
        }

        #endregion
    }
}

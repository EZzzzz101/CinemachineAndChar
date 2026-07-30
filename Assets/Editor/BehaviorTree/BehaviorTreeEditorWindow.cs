using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
using AI.BehaviourTree;

namespace AI.BehaviourTree.Editor
{
    /// <summary>
    /// 行为树可视化编辑器窗口
    /// </summary>
    public class BehaviorTreeEditorWindow : EditorWindow
    {
        // 当前正在编辑的 BehaviorTreeSO 资产
        // SerializeField 确保域重载后不丢
        [SerializeField] private BehaviorTreeSO _treeAsset;

        /// <summary>
        /// 双击 .asset 文件 → 自动打开编辑器窗口
        /// OnOpenAsset 是 Unity 提供的回调 attribute
        /// </summary>
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            // instanceID → 对应的 Object → 尝试转 BehaviorTreeSO
            var asset = EditorUtility.InstanceIDToObject(instanceID) as BehaviorTreeSO;
            if (asset == null)
                return false;  // 不是行为树资产，交给其他处理器

            OpenWindow(asset);
            return true;       // 已处理，不再往下传递
        }

        /// <summary>
        /// 菜单入口：Window → AI → 行为树编辑器
        /// </summary>
        [MenuItem("Window/AI/行为树编辑器")]
        public static void OpenWindow()
        {
            OpenWindow(null);
        }

        /// <summary>
        /// 创建/获取窗口实例
        /// 关键：先设 _treeAsset，再 Show()。这样 CreateGUI 里能读到正确的值
        /// </summary>
        public static void OpenWindow(BehaviorTreeSO asset)
        {
            // 如果传了资产，先存到窗口上（窗口可能已存在，CreateGUI 跑过了）
            var window = GetWindow<BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("行为树编辑器");

            if (asset != null)
                window._treeAsset = asset;

            // 重建画布区域 — 每次 OpenWindow 都调
            // 情况1：新窗口 → CreateGUI 刚跑完，RefreshGraphArea 再填充
            // 情况2：已有窗口 → CreateGUI 不跑，RefreshGraphArea 完成切换
            window.RefreshGraphArea();

            window.Show();
        }

        // —— UI Toolkit 元素引用 ——
        private BehaviorTreeGraphView _graphView;
        private BTInspectorView _inspectorView;
        private Label _assetLabel;
        private VisualElement _contentRow;  // 画布 + 面板 的横向容器
        private VisualElement _breadcrumbBar;   // 子图导航面包屑

        // —— Play 时高亮执行节点（像 Animator 窗口那样） ——
        private double _lastDebugPoll;

        /// <summary>
        /// CreateGUI — 骨架：工具栏 + 横向区域（画布 | 参数面板）
        /// </summary>
        private void CreateGUI()
        {
            // ===== 顶部工具栏 =====
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            _assetLabel = new Label("未打开资产");
            _assetLabel.style.color = Color.white;
            _assetLabel.style.marginLeft = 8;
            toolbar.Add(_assetLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            var saveBtn = new Button(() => Save());
            saveBtn.text = "保存";
            saveBtn.style.marginRight = 4;
            toolbar.Add(saveBtn);

            var focusBtn = new Button(() => _graphView?.FocusOnRoot());
            focusBtn.text = "定位根节点";
            focusBtn.style.marginRight = 8;
            toolbar.Add(focusBtn);

            rootVisualElement.Add(toolbar);

            // ===== 面包屑导航（文件夹钻取路径） =====
            _breadcrumbBar = new VisualElement();
            _breadcrumbBar.style.flexDirection = FlexDirection.Row;
            _breadcrumbBar.style.height = 22;
            _breadcrumbBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            _breadcrumbBar.style.paddingLeft = 8;
            _breadcrumbBar.style.paddingRight = 8;
            _breadcrumbBar.style.alignItems = Align.Center;
            rootVisualElement.Add(_breadcrumbBar);

            // ===== 横向区域：画布 + 参数面板 =====
            _contentRow = new VisualElement();
            _contentRow.style.flexDirection = FlexDirection.Row;
            _contentRow.style.flexGrow = 1f;
            rootVisualElement.Add(_contentRow);

            // 参数面板（右侧，始终存在）
            _inspectorView = new BTInspectorView();
            _contentRow.Add(_inspectorView);

            // 选中节点 → 更新参数面板
            BTNodeView.OnNodeSelected += nodeView =>
            {
                _inspectorView.Show(nodeView);
            };

            // Play 时自动高亮执行节点（像 Animator 窗口一样）
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // 面包屑在 graph view 创建后才有效，首次在 RefreshGraphArea 中触发
            if (EditorApplication.isPlaying)
                EditorApplication.update += OnEditorUpdate;

            // 域重载后 _treeAsset 通过 SerializeField 恢复，需要重建画布
            RefreshGraphArea();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _graphView?.Cleanup();
        }

        /// <summary>
        /// 根据当前 _treeAsset 重建内容区域
        /// null → 显示提示文字 / 有资产 → 显示 GraphView
        /// </summary>
        private void RefreshGraphArea()
        {
            if (_contentRow == null) return;

            // 移除旧画布（保留参数面板）
            if (_graphView != null)
            {
                _graphView.Cleanup();
                _contentRow.Remove(_graphView);
            }

            // 更新标题
            if (_assetLabel != null)
                _assetLabel.text = _treeAsset != null ? _treeAsset.name : "未打开资产";

            if (_treeAsset == null)
                return;

            _graphView = new BehaviorTreeGraphView();
            _graphView.style.flexGrow = 1f;
            _contentRow.Insert(0, _graphView);  // 画布在左，面板在右

            // 导航变化时更新面包屑
            _graphView.OnScopeChanged += UpdateBreadcrumb;

            // 标记未保存
            _graphView.OnDirty += () => hasUnsavedChanges = true;

            // 画布结构变化时刷新 WeightedRandom 的参数面板
            _graphView.OnGraphChanged += RefreshWeightedRandomInspector;

            _graphView.LoadFromSO(_treeAsset);

            // 初始更新面包屑
            UpdateBreadcrumb();
        }

        /// <summary>更新面包屑导航（从根到当前文件夹的路径）</summary>
        private void UpdateBreadcrumb()
        {
            if (_breadcrumbBar == null || _graphView == null) return;

            _breadcrumbBar.Clear();

            var crumbs = _graphView.GetBreadcrumbs();
            for (int i = 0; i < crumbs.Count; i++)
            {
                // 分隔符
                if (i > 0)
                {
                    var sep = new Label(" ▸ ");
                    sep.style.color = new Color(0.5f, 0.5f, 0.5f);
                    sep.style.fontSize = 12;
                    _breadcrumbBar.Add(sep);
                }

                var crumb = crumbs[i];
                bool isLast = (i == crumbs.Count - 1);

                if (isLast)
                {
                    // 当前所在位置（不可点击）
                    var label = new Label(crumb.DisplayName);
                    label.style.color = new Color(0.8f, 0.8f, 0.8f);
                    label.style.fontSize = 12;
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    _breadcrumbBar.Add(label);
                }
                else
                {
                    // 可点击跳转的路径节点
                    var btn = new Button(() =>
                    {
                        _graphView.ExitToFolder(crumb.FolderId);
                    });
                    btn.text = crumb.DisplayName;
                    btn.style.fontSize = 12;
                    btn.style.color = new Color(0.5f, 0.7f, 1f);
                    btn.style.backgroundColor = Color.clear;
                    btn.style.borderLeftWidth = 0;
                    btn.style.borderRightWidth = 0;
                    btn.style.borderTopWidth = 0;
                    btn.style.borderBottomWidth = 0;
                    btn.style.paddingLeft = 2;
                    btn.style.paddingRight = 2;
                    btn.style.marginLeft = 0;
                    btn.style.marginRight = 0;
                    btn.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _breadcrumbBar.Add(btn);
                }
            }

            // 如果不在根视图，添加一个"返回"按钮
            if (_graphView.IsInSubView)
            {
                var spacer = new VisualElement();
                spacer.style.flexGrow = 1f;
                _breadcrumbBar.Add(spacer);

                var backBtn = new Button(() => _graphView.ExitFolder());
                backBtn.text = "← 返回上级";
                backBtn.style.fontSize = 11;
                backBtn.style.color = new Color(0.8f, 0.6f, 0.3f);
                backBtn.style.backgroundColor = Color.clear;
                backBtn.style.borderLeftWidth = 0;
                backBtn.style.borderRightWidth = 0;
                backBtn.style.borderTopWidth = 0;
                backBtn.style.borderBottomWidth = 0;
                _breadcrumbBar.Add(backBtn);
            }
        }

        /// <summary>连线变动时刷新 WeightedRandom 的参数面板</summary>
        private void RefreshWeightedRandomInspector()
        {
            if (_graphView == null) return;
            var selected = _graphView.selection.OfType<BTNodeView>().FirstOrDefault();
            if (selected != null && selected.NodeType?.FullName == "AI.BehaviourTree.BTWeightedRandom")
            {
                // 触发重新选中，同步子节点到 Entries
                BTNodeView.SelectNode(selected);
            }
        }

        /// <summary>手动保存 / SaveChanges 时调用</summary>
        private void Save()
        {
            if (_treeAsset == null || _graphView == null) return;
            _graphView.SaveToSO(_treeAsset);
            AssetDatabase.SaveAssets();
            hasUnsavedChanges = false;
            Debug.Log($"[BT Editor] 已保存: {_treeAsset.name}");
        }

        /// <summary>Unity 在关闭窗口 / 按 Ctrl+S 时自动调用此方法</summary>
        public override void SaveChanges()
        {
            base.SaveChanges();
            Save();
        }

        // ===== 运行时高亮（像 Animator 窗口那样） =====

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshGraphArea();  // 域重载后重建画布
                EditorApplication.update += OnEditorUpdate;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= OnEditorUpdate;
                ClearDebugHighlights();
            }
        }

        private void OnEditorUpdate()
        {
            if (_graphView == null || _treeAsset == null) return;

            // 0.05 秒刷新一次（约 20fps），足够灵敏又省性能
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastDebugPoll < 0.05) return;
            _lastDebugPoll = now;

            // 找场景中使用同一棵树且正在运行的 Runner
            var runner = FindObjectOfType<BehaviorTreeRunner>();
            if (runner == null || runner.TreeAsset != _treeAsset || !Application.IsPlaying(runner))
            {
                ClearDebugHighlights();
                return;
            }

            var activeIds = runner.GetRunningNodeIds();

            var allNodeViews = _graphView.nodes.ToList().OfType<BTNodeView>().ToList();
            foreach (var nodeView in allNodeViews)
                nodeView.IsDebugActive = activeIds.Contains(nodeView.NodeId);
        }

        private void ClearDebugHighlights()
        {
            if (_graphView == null) return;
            var allNodeViews = _graphView.nodes.ToList().OfType<BTNodeView>().ToList();
            foreach (var nodeView in allNodeViews)
                nodeView.IsDebugActive = false;
        }

        // 不弹保存框，用户自己点"保存"按钮才保存
    }
}

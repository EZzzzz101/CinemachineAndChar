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
        private BehaviorTreeSO _treeAsset;

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
                _contentRow.Remove(_graphView);

            // 更新标题
            if (_assetLabel != null)
                _assetLabel.text = _treeAsset != null ? _treeAsset.name : "未打开资产";

            if (_treeAsset == null)
                return;

            _graphView = new BehaviorTreeGraphView();
            _graphView.style.flexGrow = 1f;
            _contentRow.Insert(0, _graphView);  // 画布在左，面板在右

            _graphView.LoadFromSO(_treeAsset);
        }

        private void Save()
        {
            if (_treeAsset == null || _graphView == null) return;
            _graphView.SaveToSO(_treeAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BT Editor] 已保存: {_treeAsset.name}");
        }

        /// <summary>
        /// 窗口关闭时 → 如果有未保存的修改 → 弹窗询问
        /// </summary>
        private void OnDisable()
        {
            if (_graphView == null || !_graphView.IsDirty || _treeAsset == null)
                return;

            bool save = EditorUtility.DisplayDialog(
                "未保存的更改",
                $"是否保存对 \"{_treeAsset.name}\" 的更改？",
                "保存",
                "不保存");

            if (save)
                Save();
        }
    }
}

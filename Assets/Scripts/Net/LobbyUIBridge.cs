using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 联机 UI 桥接（常驻）— 把 LobbyClientService 的网络事件接到正式 UI 上：
/// - 预加载 BeInvitedView（隐藏），保证收到邀请时弹窗已经在监听；
/// - 收到邀请 → 弹出被邀请框；
/// - 进房（JoinRoom）→ 打开组队界面，把对方放进下一个空槽位。
/// </summary>
public class LobbyUIBridge : GameModule<LobbyUIBridge>
{
    private BeInvitedView _beInvitedView;

    protected override void OnInit()
    {
        var service = LobbyClientService.Instance;
        service.OnInvited += OnInvited;
        service.OnJoinedRoom += OnJoinedRoom;

        PreloadBeInvitedView().Forget();
        Debug.Log("[LobbyUIBridge] 初始化完成");
    }

    /// <summary>预加载被邀请弹窗（隐藏实例），让它的 Awake 订阅常驻生效</summary>
    private async UniTask PreloadBeInvitedView()
    {
        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>("UI/Panels/BeInvitedView");
        if (prefab == null)
        {
            Debug.LogWarning("[LobbyUIBridge] 找不到 BeInvitedView 预制体，邀请弹窗不可用");
            return;
        }

        UIManager.Instance.EnsureRoot();
        var go = Object.Instantiate(prefab, UIManager.Instance.RootTransform);
        go.SetActive(false);
        _beInvitedView = go.GetComponent<BeInvitedView>();
    }

    private void OnInvited(string inviterName)
    {
        UIManager.Instance.Open<BeInvitedView>();
    }

    private async void OnJoinedRoom(string hostName, string guestName, string hostIp, int hostPort, int roomId)
    {
        // 进房了：关掉可能还开着的被邀请框
        if (_beInvitedView != null) _beInvitedView.Hide();

        // 打开组队界面，把"对方"放进下一个空格（自己在一号位）
        var team = await UIManager.Instance.OpenAsync<TeamUpView>();
        if (team == null) return;

        var myName = LobbyClientService.Instance.MyName;
        var other = myName == hostName ? guestName : hostName;
        team.AddMember(other);
    }
}

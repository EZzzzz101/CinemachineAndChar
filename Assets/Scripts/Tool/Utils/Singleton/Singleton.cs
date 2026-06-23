using UnityEngine;

namespace SingletonTool
{
    /// <summary>
    /// Mono 单例基类 — 场景中只能有一份，自动创建或销毁重复
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取实例：未找到则自动创建 GameObject
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    //线程锁
                    lock (_lock)
                    {
                        _instance = FindObjectOfType<T>();
                        if (_instance == null)
                        {
                            var go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 是否已初始化（不触发自动创建）
        /// </summary>
        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}

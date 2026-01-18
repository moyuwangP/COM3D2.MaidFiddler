using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // 必须引用
using BepInEx; // 👈 核心变化 1
using COM3D2.MaidFiddler.Core.IPC;
using COM3D2.MaidFiddler.Core.Utils;
using MFService = COM3D2.MaidFiddler.Core.Service.Service;

namespace COM3D2.MaidFiddler.Core
{
    // 👇 核心变化 2: 属性改为 BepInPlugin
    [BepInPlugin("com.maidfiddler.plugin", "Maid Fiddler", "2.0.0")]
    public class MaidFiddlerPlugin : BaseUnityPlugin // 👈 核心变化 3: 继承 BaseUnityPlugin
    {
        private PipeService<MFService> pipeServer;
        private MFService service;
        private bool isSceneLoading = false;

        internal string Version { get; } = "2.0.0"; // 手动写死版本号或者用 Info

        // BepInEx 的入口也是 Awake
        public void Awake()
        {
            // BepInEx 插件默认就是 DontDestroyOnLoad，但这行留着也无害
            DontDestroyOnLoad(this);

            // 👇 核心变化 4: 日志改用 BepInEx 的 Logger
            Logger.LogInfo($"Starting up Maid Fiddler {Version}");

            // 这里的 this 传进去可能需要修改 Service 的构造函数类型，下面会说
            service = new MFService(this);
            service.eventServer.ConnectionLost += OnConnectionLost;

            Logger.LogInfo("Starting server!");

            // 暂时先只开启 Service，PipeServer 也可以尝试开启
            pipeServer = new PipeService<MFService>(service, "MaidFiddlerService");
            pipeServer.ConnectionLost += OnConnectionLost;
            pipeServer.Run();

            Logger.LogInfo("Started server!");

            // 👇 注册场景加载事件 (这是 BepInEx / 新版 Unity 的标准写法)
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 标准的场景加载回调
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isSceneLoading = true;
            StartCoroutine(WaitAndInit(scene.buildIndex));
        }

        private IEnumerator WaitAndInit(int level)
        {
            // 延时 1 秒，防止 File Corrupted
            yield return new WaitForSeconds(1.0f);
            isSceneLoading = false;

            try
            {
                Logger.LogInfo($"[SafeLoad] Updating list for level {level}...");
                // 如果 Service 里有 OnLevelWasLoaded，这里调用它
                // service.OnLevelWasLoaded(level); 

                // 或者手动触发更新
                service.UpdateActiveMaidStatus();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"[SafeLoad] Error: {e.Message}");
            }
        }

        public void LateUpdate()
        {
            if (isSceneLoading) return;
            service?.UpdateActiveMaidStatus();
        }

        public void OnDestroy()
        {
            Logger.LogInfo("Stopping MaidFiddler");
            try
            {
                pipeServer?.Dispose();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error while disposing: {e}");
            }
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            Logger.LogInfo("Connection lost, resetting connection");
            if (service.eventServer.IsConnected)
                service.eventServer.Disconnect();
            service.eventServer.WaitForConnection();
        }
    }
}
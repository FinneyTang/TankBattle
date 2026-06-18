using System.Collections.Generic;
using Main;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace NeuralAI
{
    /// <summary>
    /// Runtime support for Neural tanks that are already selected in Match.TeamSettings.
    /// This never edits the Match roster or opponent pool.
    /// </summary>
    public class NeuralMatchConfigurator : MonoBehaviour
    {
        [Header("Match")]
        public bool ConfigureOnAwake = true;
        public Match TargetMatch;
        public string NeuralTankScript = "NeuralAI.TankBattleAgent";

        [Header("Inference")]
        [Tooltip("Drag the exported ONNX model here for packaged inference. Supports ML-Agents 2.x NNModel and 3.x ModelAsset.")]
        public UnityEngine.Object OverrideModel;
        public InferenceDevice Device = InferenceDevice.Default;
        public bool CreateAgentProxies = true;
        public bool ReuseExistingProxies = true;

        [Header("Agent Settings Override")]
        [Tooltip("Optional: override only NeuralAI's own runtime settings, such as decision interval and fire mode.")]
        public bool OverrideAgentSettings = false;
        public TankBattleTrainingSettings AgentSettings = new TankBattleTrainingSettings();

        [Header("Generated Objects")]
        public string ProxyRootName = "NeuralAI_Runtime";

        private readonly List<TankBattleAgentProxy> m_ConfiguredProxies = new List<TankBattleAgentProxy>(2);

        private void Awake()
        {
            if (ConfigureOnAwake)
            {
                Configure();
            }
        }

        [ContextMenu("Apply Neural Match Configuration")]
        public void Configure()
        {
            if (!EnsureMatch())
            {
                return;
            }

            m_ConfiguredProxies.Clear();
            foreach (Match.TeamSetting setting in TargetMatch.TeamSettings)
            {
                if (setting == null || !IsNeuralTankScript(setting.TankScript))
                {
                    continue;
                }

                TankBattleAgentProxy proxy = EnsureProxy(setting.Team);
                if (proxy != null)
                {
                    m_ConfiguredProxies.Add(proxy);
                }
            }
        }

        private bool EnsureMatch()
        {
            if (TargetMatch == null)
            {
                TargetMatch = GetComponent<Match>();
            }

            if (TargetMatch == null)
            {
                TargetMatch = FindObjectOfType<Match>();
            }

            if (TargetMatch == null)
            {
                Debug.LogWarning("[NeuralMatchConfigurator] No Match found in scene.");
                return false;
            }

            if (TargetMatch.TeamSettings == null || TargetMatch.TeamSettings.Count == 0)
            {
                Debug.LogWarning("[NeuralMatchConfigurator] Match has no TeamSettings. Configure contestants on Match first.");
                return false;
            }

            return true;
        }

        private bool IsNeuralTankScript(string tankScript)
        {
            return !string.IsNullOrWhiteSpace(tankScript) &&
                   tankScript.Trim() == NeuralTankScript;
        }

        private TankBattleAgentProxy EnsureProxy(ETeam team)
        {
            TankBattleAgentProxy proxy = ReuseExistingProxies ? FindExistingProxy(team) : null;
            if (proxy == null && CreateAgentProxies)
            {
                GameObject root = GetOrCreateProxyRoot();
                GameObject proxyObject = new GameObject($"AgentProxy_Team{team}");
                proxyObject.transform.SetParent(root.transform, false);
                proxy = proxyObject.AddComponent<TankBattleAgentProxy>();
            }

            if (proxy == null)
            {
                return null;
            }

            proxy.TargetTeam = team;
            proxy.AutoFindTank = true;
            proxy.OverrideModel = OverrideModel;
            proxy.Device = Device;
            proxy.OverrideAgentSettings = OverrideAgentSettings;
            proxy.AgentSettings.CopyFrom(AgentSettings);
            return proxy;
        }

        private TankBattleAgentProxy FindExistingProxy(ETeam team)
        {
            TankBattleAgentProxy[] proxies = FindObjectsOfType<TankBattleAgentProxy>();
            foreach (TankBattleAgentProxy proxy in proxies)
            {
                if (proxy != null && proxy.TargetTeam == team)
                {
                    return proxy;
                }
            }

            return null;
        }

        private GameObject GetOrCreateProxyRoot()
        {
            string rootName = string.IsNullOrWhiteSpace(ProxyRootName) ? "NeuralAI_Runtime" : ProxyRootName;
            Transform parent = TargetMatch != null ? TargetMatch.transform : transform;
            Transform existing = parent.Find(rootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            return root;
        }
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MXF
{
    /// <summary>
    ///     集中 Debug 绘制逻辑.
    ///     涵盖运行时 Debug.Draw* 线条、编辑器 Gizmos/Handles 绘制和 GUI 文字标签.
    ///     开关: EnableAttackDebug / EnableScoreDebug (false 关闭对应绘制).
    /// </summary>
    public static class Debugger
    {
        // ==================== 公共开关 ====================

        public const bool EnableAttackDebug = false;
        public const bool EnableScoreDebug = false;

        // ==================== 通用工具: 线框球体 ====================

        private const int CircleSegments = 16;

        public static void DrawWireSphere(Vector3 center, float radius, Color color, float duration)
        {
            DrawCircle(center, radius, Vector3.right, Vector3.up, color, duration);
            DrawCircle(center, radius, Vector3.right, Vector3.forward, color, duration);
            DrawCircle(center, radius, Vector3.up, Vector3.forward, color, duration);
        }

        private static void DrawCircle(Vector3 center, float r, Vector3 axis1, Vector3 axis2, Color c, float d)
        {
            for (int i = 0; i < CircleSegments; i++)
            {
                float a0 = i * 2f * Mathf.PI / CircleSegments;
                float a1 = (i + 1) * 2f * Mathf.PI / CircleSegments;
                Vector3 p0 = center + axis1 * (Mathf.Cos(a0) * r) + axis2 * (Mathf.Sin(a0) * r);
                Vector3 p1 = center + axis1 * (Mathf.Cos(a1) * r) + axis2 * (Mathf.Sin(a1) * r);
                Debug.DrawLine(p0, p1, c, d);
            }
        }

        // ==================== 策略名称映射 ====================

        public static string GetDebugActionName(UtilityActions a)
        {
            if (a is GetOneStar gos) return $"吃星 #{gos.StarID}";
            if (a is ApproachEnemy) return "接近敌人";
            if (a is FinishEnemy) return "追击敌人";
            if (a is RetreatFromEnemy) return "撤离敌人";
            if (a is ControlCenter) return "占中";
            if (a is BackHome) return "回家";
            if (a is AvoidAction) return "躲避";
            return a.GetType().Name;
        }

        // ==================== AIAttack: 运行时 Debug 绘制 ====================

        public static void DrawAttackRuntime(
            Vector3 firePos, Vector3 linearAim, Vector3 aimPos,
            Vector3 targetPos, Vector3 turretAiming)
        {
            Debug.DrawLine(firePos, linearAim, Color.white, 0.02f);
            Debug.DrawLine(linearAim, aimPos, Color.yellow, 0.02f);
            Debug.DrawLine(firePos, aimPos, Color.red, 0.02f);
            DrawWireSphere(targetPos, 1f, Color.green, 0.02f);
            DrawWireSphere(linearAim, 0.8f, Color.cyan, 0.02f);
            DrawWireSphere(aimPos, 0.6f, Color.magenta, 0.02f);
            Debug.DrawLine(firePos, firePos + turretAiming.normalized * 100f, Color.gray, 0.02f);
        }

#if UNITY_EDITOR
        // ==================== GUI 样式缓存 ====================

        private static GUIStyle _debugLabelStyle;
        private static GUIStyle _attackDebugStyle;
        private static GUIStyle _debugEndpointStyle;

        // ==================== MyTank: 完整 Gizmos 入口 ====================

        public static void DrawMyTankGizmos(MyTank tank)
        {
            DrawScoreTextGizmos(tank);
            DrawAttackGizmos(tank);
            if (tank.CurrentAction is AvoidAction fa)
                DrawAvoidGizmos(fa);
        }

        // ---- 策略评分文字 ----

        private static void DrawScoreTextGizmos(MyTank tank)
        {
            if (!EnableScoreDebug || string.IsNullOrEmpty(tank.DebugScoresText)) return;

            if (_debugLabelStyle == null)
            {
                _debugLabelStyle = new GUIStyle
                {
                    richText = true,
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                _debugLabelStyle.normal.textColor = Color.white;
            }

            Vector3 offset = (tank.transform.forward + tank.transform.right).normalized * 4f + Vector3.up * 3f;
            Vector3 labelPos = tank.transform.position + offset;
            Handles.Label(labelPos, tank.DebugScoresText, _debugLabelStyle);
        }

        // ---- 贝叶斯校正偏移可视化 ----

        private static void DrawAttackGizmos(MyTank tank)
        {
            AIAttack attack = tank.AttackForDebug;
            if (attack == null || !attack.DebugHasData) return;

            if (_attackDebugStyle == null)
            {
                _attackDebugStyle = new GUIStyle
                {
                    richText = true,
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
            }

            string info = string.Format(
                "<color=#FFD700>Mu(偏移): {0:+0.00;-0.00}m</color>",
                attack.Mu);

            Transform t = tank.transform;
            Vector3 infoOffset = Vector3.up * 5f + (t.forward + t.right).normalized * 20f;
            Handles.Label(t.position + infoOffset, info, _attackDebugStyle);

            Handles.color = Color.white;
            Handles.DrawDottedLine(attack.DebugFirePos, attack.DebugLinearAim, 4f);
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(attack.DebugLinearAim, attack.DebugCorrectedAim, 4f);

            if (attack.DebugCorrectionVec.magnitude > 0.001f)
            {
                Vector3 midPoint = (attack.DebugLinearAim + attack.DebugCorrectedAim) * 0.5f;
                Handles.Label(midPoint + Vector3.up * 0.5f,
                    string.Format("<color=#FFD700>Mu={0:+0.00;-0.00}m</color>", attack.Mu),
                    _attackDebugStyle);
            }
        }

        // ---- AvoidAction: 自由躲避方向枚举可视化 ----

        public static void DrawAvoidGizmos(AvoidAction action)
        {
            float[] minDists = action.DebugMinDists;
            if (minDists == null || minDists.Length != AvoidAction.AngleSteps) return;

            if (_debugEndpointStyle == null)
            {
                _debugEndpointStyle = new GUIStyle
                {
                    normal = { textColor = Color.white },
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            Vector3 tankPos = action.DebugTankPos;
            float stepDeg = 360f / AvoidAction.AngleSteps;

            for (int i = 0; i < AvoidAction.AngleSteps; i++)
            {
                float rad = i * stepDeg * Mathf.Deg2Rad;
                Vector3 dir3D = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                Vector3 endpoint = tankPos + dir3D * AvoidAction.TargetRadius;
                float minDist = minDists[i];

                Color color;
                bool blocked = minDist < 0f;
                if (blocked) color = new Color(0.35f, 0.35f, 0.35f, 0.4f);
                else if (minDist < AvoidAction.MinSafeDist) color = new Color(1f, 0.2f, 0.2f, 0.7f);
                else if (minDist < AvoidAction.DesiredSafeDist) color = new Color(1f, 0.85f, 0f, 0.65f);
                else color = new Color(0f, 0.75f, 0f, 0.65f);

                if (i == action.DebugBestIdx) color.a = 1f;
                Handles.color = color;
                Handles.DrawLine(tankPos, endpoint);
                Handles.DrawSolidDisc(endpoint, Vector3.up, i == action.DebugBestIdx ? 0.2f : blocked ? 0.04f : 0.07f);

                string label = blocked ? $"#{i} BLK" : minDist >= float.MaxValue * 0.5f ? $"#{i} -" : $"#{i} {minDist:F1}m";
                Handles.Label(endpoint + Vector3.up * 0.4f, label, _debugEndpointStyle);
            }

            Handles.color = new Color(0f, 0.7f, 0f, 0.2f);
            Handles.DrawWireDisc(tankPos, Vector3.up, AvoidAction.DesiredSafeDist);
            Handles.color = new Color(1f, 0.2f, 0.2f, 0.25f);
            Handles.DrawWireDisc(tankPos, Vector3.up, AvoidAction.MinSafeDist);
        }
#endif
    }
}

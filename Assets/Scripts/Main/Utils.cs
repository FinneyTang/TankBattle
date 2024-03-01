using UnityEngine;

namespace Main
{
    public static class Utils
    {
        public static void PlayParticle(string resName, Vector3 pos)
        {
            Object obj = Resources.Load(resName);
            if(obj == null)
            {
                return;
            }
            GameObject effect = (GameObject)Object.Instantiate(obj);
            if(effect != null)
            {
                effect.transform.position = pos;
            }
        }
        public static Color GetTeamColor(ETeam t)
        {
            switch (t)
            {
                case ETeam.A:
                    return Color.red;
                case ETeam.B:
                    return Color.cyan;
                case ETeam.C:
                    return Color.green;
                case ETeam.D:
                    return Color.magenta;
            }
            return Color.white;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace kerbal_impact
{
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    class KerbalImpact : MonoBehaviour
    {
        void Start()
        {
            ImpactMonitor.getInstance().Start();
        }

        void Stop()
        {
            ImpactMonitor.getInstance().Stop();
        }

    }
}

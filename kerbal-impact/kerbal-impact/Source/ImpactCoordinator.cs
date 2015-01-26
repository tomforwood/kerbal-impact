using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace kerbal_impact
{
    public sealed class ImpactCoordinator
    {
        private static readonly ImpactCoordinator instance = new ImpactCoordinator();


        public EventData<ImpactScienceData> scienceListeners = new EventData<ImpactScienceData>("ScienceReceived");
        public EventData<ImpactScienceData> bangListeners = new EventData<ImpactScienceData>("BigImpact");

        private ImpactCoordinator() { }
        public static ImpactCoordinator getInstance()
        {
            return instance;
        }
    }
}

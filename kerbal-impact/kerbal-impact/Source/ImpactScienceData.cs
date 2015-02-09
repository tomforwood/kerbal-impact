using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace kerbal_impact
{
    public class ImpactScienceData : ScienceData
    {

        private const string energyName = "KineticEnergy";
        private const string biomeName = "Biome";
        private const string latName = "Latitude";

        public float kineticEnergy { get; private set;}
        public string biome { get; private set; }
        public double latitude { get; private set; }

        public ImpactScienceData(ConfigNode node) : base(node)
        {
            LoadImpact(node);
        }

        public ImpactScienceData(float energy, String biome, double latitude, float amount, float xmitValue, float labBoost, String id, String dataname)
            : base(amount, xmitValue, labBoost, id, dataname)
        {
            kineticEnergy = energy;
            this.biome = biome;
            this.latitude = latitude;
        }

        public void LoadImpact(ConfigNode node)
        {
            base.Load(node);
            if (node.HasValue(energyName))
            {
                kineticEnergy = float.Parse(node.GetValue(energyName));
            }
            if (node.HasValue(biomeName))
            {
                biome = node.GetValue(biomeName);
            }
            if (node.HasValue(latName))
            {
                latitude = float.Parse(node.GetValue(latName));
            }
        }

        public void SaveImpact(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(energyName, kineticEnergy);
            if (biome != null)
            {
                node.AddValue(biomeName, biome);
            }
            node.AddValue(latName, latitude);
        }
    }
}

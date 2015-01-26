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

        public float kineticEnergy { get; private set;}
        public string biome { get; private set; }

        public ImpactScienceData(ConfigNode node) : base(node)
        {
            Load(node);
        }

        public ImpactScienceData(float energy, float amount, float xmitValue, float labBoost, String id, String dataname)
            : base(amount, xmitValue, labBoost, id, dataname)
        {
            kineticEnergy = energy;
        }
        public ImpactScienceData(String biome, float amount, float xmitValue, float labBoost, String id, String dataname)
            : base(amount, xmitValue, labBoost, id, dataname)
        {
            this.biome = biome;
        }

        public new void Load(ConfigNode node)
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
        }

        public void Save(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(energyName, kineticEnergy);
            if (biome != null)
            {
                node.AddValue(biomeName, biome);
            }
        }
    }
}

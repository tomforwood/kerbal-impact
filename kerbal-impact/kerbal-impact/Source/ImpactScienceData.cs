using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace kerbal_impact
{
    public class ImpactScienceData : ScienceData
    {
        public enum DataTypes{Seismic, Spectral, Asteroid};

        private const string energyName = "KineticEnergy";
        private const string biomeName = "Biome";
        private const string latName = "Latitude";
        private const string astName = "Asteroid";
        public const String DataTypeName = "DataType";

        public float kineticEnergy { get; private set;}
        public string biome { get; private set; }
        public double latitude { get; private set; }
        public string asteroid { get; private set; }
        public DataTypes datatype { get; private set; }

        public ImpactScienceData(ConfigNode node) : base(node)
        {
            LoadImpact(node);
        }

		public ImpactScienceData(DataTypes dataType, float energy, String biome, double latitude, float amount, float xmitValue, float labBoost, String id, String dataname, bool triggered, uint flightID)
			: base(amount, xmitValue, labBoost, id, dataname, triggered, flightID)
        {
            this.datatype = dataType;
            kineticEnergy = energy;
            this.biome = biome;
            this.latitude = latitude;
        }
		public ImpactScienceData(float energy, String asteroid, float amount, float xmitValue, float labBoost, String id, String dataname, bool triggered, uint flightID)
			: base(amount, xmitValue, labBoost, id, dataname, triggered, flightID)
        {
            kineticEnergy = energy;
            this.asteroid = asteroid;
            this.datatype = DataTypes.Asteroid;
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
            if (node.HasValue(astName))
            {
                asteroid = node.GetValue(astName);
            }
            if (node.HasValue(DataTypeName))
            {
                datatype = (DataTypes) Enum.Parse(typeof(DataTypes), node.GetValue(DataTypeName));
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
            if (asteroid !=null )
            {
                node.AddValue(astName, asteroid);
            }
            node.AddValue(DataTypeName, datatype);
            node.AddValue(latName, latitude);
        }
    }
}

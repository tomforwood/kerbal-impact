﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Contracts;
using KSP;
using KSPAchievements;

namespace kerbal_impact
{

    class ImpactContract : Contract
    {
        const String valuesNode = "ContractValues";
        
        protected static Dictionary<ContractPrestige, int> starRatings = new Dictionary<ContractPrestige, int> 
        { {ContractPrestige.Trivial,1 },{ContractPrestige.Significant, 2},{ContractPrestige.Exceptional, 3}};

        protected PossibleContract pickedContract;
        protected readonly System.Random random = new System.Random();

        public int randId = new System.Random().Next();
        

        protected override bool Generate()
        {
            return false;
        }

        protected bool actuallyGenerate(){
            IEnumerable<CelestialBody> bodies = Contract.GetBodies_Reached(false, false);
            
            bodies = bodies.Where(body => !body.atmosphere);
            //generate a weighted list of possible contracts (different bodsies and biomes where appropriate)
            List<PossibleContract> contracts = pickContracts(bodies);
            if (contracts.Count == 0) return false;
            double totalProb = contracts.Last().probability;
            double picked = random.NextDouble()*totalProb;
            IComparer<PossibleContract> comp = new PossibleContract.ProbComparer();
            int contractIndex = contracts.BinarySearch(new PossibleContract(picked, null, 0), comp);
            if (contractIndex < 0) contractIndex = ~contractIndex;
            //ImpactMonitor.Log("pickedindex=" + contractIndex);
            pickedContract = contracts[contractIndex];
            ImpactMonitor.Log("picked one "+pickedContract);

            //TODO all of these
            SetExpiry();
            SetScience(1.5f, pickedContract.body);
            SetDeadlineYears(0.5f, pickedContract.body);
            SetReputation(3, 4, pickedContract.body);
            SetFunds(20000,80000,10000,pickedContract.body);

            generateParameters();
            ImpactMonitor.Log("Generated parameters");
            
            return true;
        }

        protected void generateParameters()
        {
            AddParameter(new ImpactParameter(pickedContract));
            AddParameter(new ScienceReceiptParameter(pickedContract));
        }
        protected virtual List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies) { return null; }

        public override bool CanBeCancelled()
        {
            return true;
        }

        public override bool CanBeDeclined()
        {
            return true;
        }

        protected override string GetHashString()
        {
            return pickedContract.getHashString();
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            pickedContract = new PossibleContract(node.GetNode(valuesNode));
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            ConfigNode paramNode = new ConfigNode(valuesNode);
            pickedContract.save(paramNode);
            node.AddNode(paramNode);
        }

        protected override void OnCompleted()
        {
            ImpactMonitor.Log("Completed contract with id " + randId);
            base.OnCompleted();

        }

        public class PossibleContract
        {
            public double probability;
            public CelestialBody body;
            public double energy;
            public String biome;
            public double latitude;
            public string asteroid;
            public ImpactScienceData.DataTypes expectedDataType;

            public PossibleContract(double prob, CelestialBody bod, double energy)
            {
                probability = prob;
                body = bod;
                this.energy = energy;
                expectedDataType = ImpactScienceData.DataTypes.Seismic;
            }

            public PossibleContract(double prob, CelestialBody bod, string biome, float latitude)
            {
                probability = prob;
                body = bod;
                this.biome = biome;
                this.latitude = latitude;
                expectedDataType = ImpactScienceData.DataTypes.Spectral;
            }

            public PossibleContract(double prob, string asteroid)
            {
                probability = prob;
                this.asteroid = asteroid;
                expectedDataType = ImpactScienceData.DataTypes.Asteroid;
            }

            public override String ToString()
            {
                if (body != null) { return body.theName + "-" + ImpactMonitor.energyFormat(energy) + "-" + biome + "-" + latitude; }
                else return asteroid;
                
            }


            public PossibleContract(ConfigNode node)
            {
                if (node.HasValue("BodyName"))
                {
                    String bodyName = node.GetValue("BodyName");
                    body = FlightGlobals.Bodies.Find(b => b.name == bodyName);
                }
                if (node.HasValue("Energy")) {
                    energy = Double.Parse(node.GetValue("Energy"));
                }
                if (node.HasValue("Biome")) {
                    biome = node.GetValue("Biome");
                }
                if (node.HasValue("Latitude"))
                {
                    latitude = float.Parse(node.GetValue("Latitude"));
                }
                if (node.HasValue("Asteroid"))
                {
                    asteroid = node.GetValue("Asteroid");
                }
                if (node.HasValue(ImpactScienceData.DataTypeName))
                {
                    expectedDataType = (ImpactScienceData.DataTypes)
                        Enum.Parse(typeof(ImpactScienceData.DataTypes), 
                        node.GetValue(ImpactScienceData.DataTypeName));
                }
                else
                {
                    //load up legacy contracts which didn't save datatype
                    if (biome != null || latitude > 0) expectedDataType = ImpactScienceData.DataTypes.Spectral;
                    else if (asteroid != null) expectedDataType = ImpactScienceData.DataTypes.Asteroid;
                    else expectedDataType = ImpactScienceData.DataTypes.Seismic;
                }
                ImpactMonitor.Log("Loaded datatype is " + expectedDataType);
            }

            public void save(ConfigNode node)
            {
                if (body != null)
                {
                    node.AddValue("BodyName", body.name);
                }
                if (energy != 0)
                {
                    node.AddValue("Energy", energy);
                }
                if (biome != null)
                {
                    node.AddValue("Biome", biome);
                }
                if (latitude != 0)
                {
                    node.AddValue("Latitude", latitude);
                }
                if (asteroid != null)
                {
                    node.AddValue("Asteroid", asteroid);
                }
                node.AddValue(ImpactScienceData.DataTypeName, expectedDataType);
            }

            public class ProbComparer : IComparer<PossibleContract>
            {
                public int Compare(PossibleContract p1, PossibleContract p2)
                {
                    return p1.probability.CompareTo(p2.probability);
                }
            }

            internal string getHashString()
            {
                if (body != null)
                {
                    return body.name + energy + biome;
                }
                else return asteroid;
            }
        }
    }

    class SeismicContract :  ImpactContract
    {
        private const String titleBlurb = "Record an impact with a seismometer of {0} on {1}";
        private const String descriptionBlurb = "We all like big bangs - and the scientists tell us they can be usefull.\n Crash a probe into {0} with at least {1}" +
            " of kinetic energy and record the results with a sesimometer landed on {0}";

        protected override bool Generate()
        {
            return actuallyGenerate();
        }
        
        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;

            foreach (CelestialBody body in bodies)
            {
                IEnumerable<SeismicContract> contracts = ContractSystem.Instance.GetCurrentContracts<SeismicContract>()
                    .Where(contract => contract.pickedContract.body == body);
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given body at once
                contracts = ContractSystem.Instance.GetCurrentContracts<SeismicContract>()
                    .Where(contract => contract.prestige == prestige && contract.ContractState == State.Offered);
                if (contracts.Count() > 0) continue;//only 1 contract a given prestige offered at a time

                ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");

                ScienceSubject subject;
                ExperimentSituations sit = ExperimentSituations.SrfLanded;
                subject = ResearchAndDevelopment.GetExperimentSubject(experiment, sit, body, "surface");
                int stars = starRatings[prestige];
                double energy = pickKE(stars, subject, body);
                possible.Add(new PossibleContract(++probSum, body, energy));
            }
            return possible;
        }

        private double pickKE(double stars, ScienceSubject subject, CelestialBody body) {
            double scienceCap = subject.scienceCap;
            double minSci = (stars-1)/3*scienceCap;
            double maxSci = stars/3*scienceCap;
            double goalScience = minSci + random.NextDouble() *  (maxSci - minSci);
            double ke = ImpactMonitor.translateScienceToKE(goalScience, body, subject);
            return ke;
        }

        protected override string GetTitle()
        {
            return String.Format(titleBlurb, ImpactMonitor.energyFormat(pickedContract.energy), pickedContract.body.theName);
        }

        protected override string GetDescription()
        {
            return String.Format(descriptionBlurb, pickedContract.body.theName, ImpactMonitor.energyFormat(pickedContract.energy));
        }

        protected override string GetSynopsys()
        {
            return GetTitle();
        }

        protected override string MessageCompleted()
        {
            return "Science data received";
        }

        public override bool MeetRequirements()
        {
            AvailablePart ap = PartLoader.getPartInfoByName("Impact Seismometer");
            if (ap != null)
            {
                if (ResearchAndDevelopment.PartTechAvailable(ap))
                {
                    return true;
                }
            }
            return false;
        }
    }

    class SpectrumContract : ImpactContract
    {
        private static Dictionary<CelestialBody, Dictionary<String, int>> biomeDifficulties;
        private static String configFile = KSPUtil.ApplicationRootPath + "GameData/Impact/biomedifficulty.cfg";
        private static bool useBiomes;

        private const String titleBlurb = "Record an impact with a Spectrometer in {0} on {1}";
        private const String descriptionBlurb = "We all like big bangs - and the scientists tell us they can be useful.\n Crash a probe into {0} on {1}" +
            " and observe the results with a spectrometer in orbit";

        private const String titleLatBlurb = "Record an impact with a Spectrometer into {0} above {1}°(N/S)";
        private const String descriptionLatBlurb = "We all like big bangs - and the scientists tell us they can be useful.\n Crash a probe into {0} above" +
            "{1}° Latitude North or South and observe the results with a spectrometer in orbit";

        protected override bool Generate()
        {
            if (biomeDifficulties == null)
            {
                loadDifficulties();
            }
            return actuallyGenerate();
        }

        private static void loadDifficulties()
        {
            ImpactMonitor.Log("Loading difficulties from "+configFile);
            ConfigNode node = ConfigNode.Load(configFile);

            if (node.HasValue("use_spectrum_biomes"))
            {
                useBiomes = bool.Parse(node.GetValue("use_spectrum_biomes"));
            }

            if (node.HasNode("BIOMES_LIST"))
            {
                biomeDifficulties = new Dictionary<CelestialBody, Dictionary<string, int>>();
                foreach (ConfigNode bodyNode in node.GetNodes())
                {
                    String bodyName = bodyNode.GetValue("body");
                    CelestialBody body = FlightGlobals.Bodies.Find( b => b.name == bodyName);
                    Dictionary<string, int> difficulties = new Dictionary<string, int>();
                    ConfigNode.ValueList values = bodyNode.values;
                    foreach (ConfigNode.Value s in values)
                    {
                        if (s.name == "body") continue;
                        difficulties.Add(s.name, int.Parse(s.value));

                    }
                    biomeDifficulties.Add(body, difficulties);

                }
            }
        }

        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;
            foreach (CelestialBody body in bodies)
            {
                //ImpactMonitor.Log("posible body=" + body.theName);
                IEnumerable<SpectrumContract> contracts = ContractSystem.Instance.GetCurrentContracts<SpectrumContract>()
                    .Where(contract => contract.pickedContract.body == body);
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given body at once

                contracts = ContractSystem.Instance.GetCurrentContracts<SpectrumContract>()
                    .Where(contract => contract.prestige == prestige && contract.ContractState==State.Offered);
                if (contracts.Count() > 0) continue;//only 1 contract a given prestige offered at a time


                //ImpactMonitor.Log("posible body="+body.theName);
                if (!biomeDifficulties.ContainsKey(body)) continue;
                Dictionary<string, int> biomes = biomeDifficulties[body];
                int stars = starRatings[prestige];
                //ImpactMonitor.Log("Looking for contracs with stars" + stars);
                if (useBiomes)
                {
                    IEnumerable<KeyValuePair<String, int>> b = biomes.Where(bd => (int)(bd.Value / 3.4) == stars - 1);
                    foreach (KeyValuePair<String, int> biomeVal in b)
                    {
                        string biome = biomeVal.Key;
                        //ImpactMonitor.Log("contract stars = " + stars + " possible biome =" + biome);
                        possible.Add(new PossibleContract(probSum++, body, biome, 0));
                    }
                }
                else
                {
                    float lat=0;
                    switch (prestige)
                    {
                        case ContractPrestige.Trivial:
                            lat = 0;
                            break;
                        case ContractPrestige.Significant:
                            lat = 50;
                            break;
                        case ContractPrestige.Exceptional:
                            lat = 75;
                            break;
                    }
                    possible.Add(new PossibleContract(probSum++, body, null, lat));
                }

            }
            return possible;
        }

        protected override string GetTitle()
        {
            if (useBiomes)
            {
                return String.Format(titleBlurb, pickedContract.biome, pickedContract.body.theName);
            }
            else
            {
                return String.Format(titleLatBlurb, pickedContract.body.theName, pickedContract.latitude);
            }
        }

        protected override string GetDescription()
        {
            if (useBiomes)
                return String.Format(descriptionBlurb, pickedContract.biome, pickedContract.body.theName);
            else
                return String.Format(descriptionLatBlurb, pickedContract.body.theName, pickedContract.latitude);
        }

        protected override string GetSynopsys()
        {
            return GetTitle();
        }

        protected override string MessageCompleted()
        {
            return "Science data received";
        }

        public override bool MeetRequirements()
        {
            AvailablePart ap = PartLoader.getPartInfoByName("Impact Spectrometer");
            if (ap != null)
            {
                if (ResearchAndDevelopment.PartTechAvailable(ap))
                    return true;
            }
            return false;
        }
    }

    class AsteroidSpectrumContract : ImpactContract
    {
        private const String titleBlurb = "Record an impact with a Spectrometer with asteroid {0}";
        private const String descriptionBlurb = "We all like big bangs - and the scientists tell us they can be usefull.\n Crash a probe into asteroid {0}" +
            " and observe the results with a spectrometer in orbit.\nThe spectometer must be within 500km of the impact";

        protected override bool Generate()
        {
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            bool result = actuallyGenerate();
            if (result)
            {
                //make the name of the targeted asteroid visible
                IEnumerable<Vessel> asteroids =
                FlightGlobals.Vessels.Where(v => v.GetName() == pickedContract.asteroid);
                Vessel asteroid = asteroids.First();
                asteroid.DiscoveryInfo.SetLevel(DiscoveryLevels.Name | DiscoveryLevels.Presence);
            }
            return result;
        }

        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;
            IEnumerable<Vessel> asteroids= FlightGlobals.Vessels.Where(v => v.vesselType == VesselType.SpaceObject);
            foreach (Vessel asteroid in asteroids)
            {
                ImpactMonitor.Log("asteroid name = " + asteroid.GetName() + " asteroid discovery=" + asteroid.DiscoveryInfo.Level);
                IEnumerable<AsteroidSpectrumContract> contracts = ContractSystem.Instance.GetCurrentContracts<AsteroidSpectrumContract>()
                    .Where(contract => contract.pickedContract.asteroid == asteroid.GetName());
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given asteroid at once

                contracts = ContractSystem.Instance.GetCurrentContracts<AsteroidSpectrumContract>()
                    .Where(contract => contract.prestige == prestige && contract.ContractState == State.Offered);
                if (contracts.Count() > 0) continue;//only 1 contract a given prestige offered at a time
                
                //Does this asteroid match the correct presige?
                int stars = getAsteroidStars(asteroid);
                if (stars == starRatings[prestige])
                {
                    possible.Add(new PossibleContract(probSum++, asteroid.GetName()));
                }

            }
            return possible;
        }

        private int getAsteroidStars(Vessel asteroid)
        {
            int stars = 2;
            //get size class  - a=3, b,c=2, d,e=1

            stars += orbitFactor(asteroid.orbit.referenceBody);

            stars = Math.Max(1, Math.Min(3, stars));
            return stars;


        }

        private int orbitFactor(CelestialBody celestialBody)
        {
            if (celestialBody.GetName() == "Kerbin") return -1;
            if (celestialBody.GetName() == "Sun") return 0;
            return orbitFactor(celestialBody.GetOrbit().referenceBody) + 1;
        }

        protected override string GetTitle()
        {
           return String.Format(titleBlurb, pickedContract.asteroid);
        }

        protected override string GetDescription()
        {
            return String.Format(descriptionBlurb, pickedContract.asteroid);
        }

        protected override string GetSynopsys()
        {
            return GetTitle();
        }

        protected override string MessageCompleted()
        {
            return "Science data received";
        }

        public override bool MeetRequirements()
        {
            AvailablePart ap = PartLoader.getPartInfoByName("Impact Spectrometer");
            if (ap != null)
            {
                if (ResearchAndDevelopment.PartTechAvailable(ap))
                    return true;
            }
            return false;
        }

        private void OnVesselDestroy(Vessel vessel)
        {
            ImpactMonitor.Log("In astContract onVesselDestroy");
            if (vessel.vesselType == VesselType.SpaceObject)
            {
                ImpactMonitor.Log("vessel of type asteroid has beenb destroyes - checking for active contracts");
                ImpactMonitor.Log("PC="+pickedContract);
                ImpactMonitor.Log("PC.ast=" + pickedContract.asteroid);
                ImpactMonitor.Log("vesssle=" + vessel);
                
                if (pickedContract!=null && pickedContract.asteroid != null && pickedContract.asteroid == vessel.GetName())
                {
                    ImpactMonitor.Log("the asteroid is the one refered to by thius contract");
                    this.Cancel();
                }
            }
            ImpactMonitor.Log("exiting astContract onVesselDestroy");
        }

        protected override void OnFinished()
        {
            base.OnFinished();
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
        }

        protected override void OnAccepted()
        {
            base.OnAccepted();
            IEnumerable<Vessel> asteroids = 
                FlightGlobals.Vessels.Where(v => v.GetName() == pickedContract.asteroid);
            Vessel asteroid = asteroids.First();
            asteroid.DiscoveryInfo.SetLevel(DiscoveryLevels.StateVectors | DiscoveryLevels.Name | DiscoveryLevels.Presence);
            
        }


    }

    class ImpactParameter : ContractParameter
    {
        private const string keTitle = "Crash into {0} with {1}";
        private const string biomeTitle = "Crash into {0} on {1}";
        private const string latitudeTitle = "Crash into {0} above {1}° (N/S)";
        private const String asteroidTitle = "Crash into {0}";

        ImpactContract.PossibleContract contract;
        private Boolean isComplete = false;

        public ImpactParameter()
        {

        }
        
        public ImpactParameter(ImpactContract.PossibleContract contract) {
            this.contract = contract;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            ImpactCoordinator.getInstance().bangListeners.Add(OnBang);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
        }

        private void OnBang(ImpactScienceData data)
        {
            if (isComplete)
            {
                ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
            }
            ImpactMonitor.Log("bang received in " + contract.expectedDataType + " parameter " + data.datatype);
            if (data.datatype != contract.expectedDataType) return;
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(data.subjectID);

            bool passed = false;
            switch (contract.expectedDataType)
            {
                case ImpactScienceData.DataTypes.Seismic:
                    //check this was the right body and the impact was high enough energy
                    passed = (subject.IsFromBody(contract.body) && data.kineticEnergy >= contract.energy);
                    break;
                case ImpactScienceData.DataTypes.Spectral:
                    //if a biome is specified  then check the biome matches
                    ImpactMonitor.Log("Contract biome =" + contract.biome + " data biome =" + data.biome);
                    ImpactMonitor.Log("Contract lat =" + contract.latitude + " data lat =" + data.latitude);
                    if (contract.biome != null)
                    {
                        passed = data.biome == contract.biome;
                    }
                    else
                    {
                        passed = contract.latitude <= Math.Abs(data.latitude);
                    }
                    break;
                case ImpactScienceData.DataTypes.Asteroid:
                    ImpactMonitor.Log("Contract astreroid =" + contract.asteroid + " data asteroid ="
                    + data.asteroid + "data.datatype =" + data.datatype + " data asteroid =" + data.asteroid);
                    passed= contract.asteroid==data.asteroid;
                    break;
            }

            if (passed) {
                SetComplete();
                isComplete = true;
                ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
            }
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            contract = new ImpactContract.PossibleContract(node);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            contract.save(node);
        }

        protected override string GetTitle()
        {
            if (contract.asteroid != null)
            {
                return String.Format(asteroidTitle, contract.asteroid);
            }
            if (contract.biome == null) {
                if (contract.energy > 0)
                {
                    return String.Format(keTitle, contract.body.theName, ImpactMonitor.energyFormat(contract.energy));
                }
                else
                {
                    return String.Format(latitudeTitle, contract.body.theName, contract.latitude);
                }
            } 
            else
                return String.Format(biomeTitle, contract.biome, contract.body.theName);
        }
    }

    class ScienceReceiptParameter : ContractParameter
    {
        private const string keTitle = "Recover science data";

        private Boolean isComplete = false;

        ImpactContract.PossibleContract contract;

        long randId;

        public ScienceReceiptParameter()
        {
            randId = (new System.Random()).Next();
        }

        public ScienceReceiptParameter(ImpactContract.PossibleContract contract)
        {
            
            this.contract = contract;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            ImpactCoordinator.getInstance().scienceListeners.Add(OnScience);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            ImpactCoordinator.getInstance().scienceListeners.Remove(OnScience);
        }

        private void OnScience(ImpactScienceData data)
        {
            if (isComplete)
            {
                ImpactCoordinator.getInstance().scienceListeners.Remove(OnScience);
            }
            ImpactMonitor.Log("science received in "+contract.expectedDataType+" parameter " + randId);
            if (data.datatype != contract.expectedDataType) return;
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(data.subjectID);
            

            bool passed = false;
            switch (contract.expectedDataType)
            {
                case ImpactScienceData.DataTypes.Seismic:
                    //check this was the right body and the impact was high enough energy
                    passed = (subject.IsFromBody(contract.body) && data.kineticEnergy >= contract.energy);
                    break;
                case ImpactScienceData.DataTypes.Spectral:
                    //if a biome is specified  then check the biome matches
                    ImpactMonitor.Log("Contract biome =" + contract.biome + " data biome =" + data.biome);
                    if (contract.biome != null)
                    {
                        passed = data.biome == contract.biome;
                    }
                    else passed = contract.latitude <= Math.Abs(data.latitude);
                    break;
                case ImpactScienceData.DataTypes.Asteroid:
                    ImpactMonitor.Log("Contract astreroid =" + contract.asteroid + " data asteroid ="
                    + data.asteroid + "data.datatype =" + data.datatype + " data asteroid =" + data.asteroid);
                    passed= contract.asteroid==data.asteroid;
                    break;
            }
            if (passed)
            {
                SetComplete();
                isComplete = true;
                ImpactCoordinator.getInstance().scienceListeners.Remove(OnScience);
            }
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            contract = new ImpactContract.PossibleContract(node);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            contract.save(node);
        }

        protected override string GetTitle()
        {
            return keTitle;
        }
    }
}

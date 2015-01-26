using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Contracts;
//using Contracts.Parameters;
using KSP;
using KSPAchievements;

namespace kerbal_impact
{

    class ImpactContract : Contract
    {
        const String valuesNode = "ContractValues";
        static Dictionary<CelestialBody, Dictionary<ContractPrestige, HashSet<String>>> biomeDifficulties;
        static string[] instruments = { "ImpactSeismometer", "ImpactSpectrometer" };
        protected static Dictionary<ContractPrestige, int> starRatings = new Dictionary<ContractPrestige, int> 
        { {ContractPrestige.Trivial,1 },{ContractPrestige.Significant, 2},{ContractPrestige.Exceptional, 3}};

        protected PossibleContract pickedContract;

        
        protected readonly System.Random random = new System.Random();

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
            pickedContract = contracts[contractIndex];
            ImpactMonitor.Log("picked one");

            //TODO all of these
            SetExpiry();
            SetScience(15, pickedContract.body);
            SetDeadlineYears(2, pickedContract.body);
            SetReputation(3, -4, pickedContract.body);
            SetFunds(5,6,-7,pickedContract.body);

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
            ImpactMonitor.Log(node.ToString());
            pickedContract = new PossibleContract(node.GetNode(valuesNode));
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            ConfigNode paramNode = new ConfigNode(valuesNode);
            pickedContract.save(paramNode);
            node.AddNode(paramNode);
        }

        public class PossibleContract
        {
            public double probability;
            public CelestialBody body;
            public double energy;
            public String biome;

            public PossibleContract(double prob, CelestialBody bod, double energy)
            {
                probability = prob;
                body = bod;
                this.energy = energy;
            }

            public PossibleContract(ConfigNode node)
            {
                ImpactMonitor.Log("Loading PC");
                String bodyName = node.GetValue("BodyName");
                ImpactMonitor.Log("body name ="+bodyName);
                body = FlightGlobals.Bodies.Find( b => b.name == bodyName);
                if (node.HasValue("Energy")) {
                    energy = Double.Parse(node.GetValue("Energy"));
                }
                if (node.HasValue("Biome")) {
                    biome = node.GetValue("Biome");
                }
            }

            public void save(ConfigNode node)
            {
                node.AddValue("BodyName", body.name);
                if (energy != 0)
                {
                    node.AddValue("Energy", energy);
                }
                if (biome != null)
                {
                    node.AddValue("Biome", biome);
                }
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
                return body.name + energy + biome;
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
            foreach (CelestialBody body in bodies)
            {
                IEnumerable<SeismicContract> contracts = ContractSystem.Instance.GetCurrentContracts<SeismicContract>().Where(contract => contract.pickedContract.body == body);
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given body at once
                //I guess you could have differnt biomes though //TODO
                ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");
                    
                ScienceSubject subject;
                ExperimentSituations sit = ExperimentSituations.SrfLanded;
                subject = ResearchAndDevelopment.GetExperimentSubject(experiment, sit, body, "surface");
                int stars = starRatings[prestige];
                double energy = pickKE(stars, subject, body);
                possible.Add(new PossibleContract(1, body, energy));
            }
            return possible;
        }

        private double pickKE(double stars, ScienceSubject subject, CelestialBody body) {
            ImpactMonitor.Log("picking KE stars=" + stars);
            double scienceCap = subject.scienceCap;
            ImpactMonitor.Log("Subjectcap = " + scienceCap);
            double minSci = (stars-1)/3*scienceCap;
            double maxSci = stars/3*scienceCap;
            ImpactMonitor.Log("minSci=" + minSci + " maxSci="+maxSci);
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
            //TODO tie to part discovery
            return true;
            //return base.MeetRequirements();
        }
    }

    /*class SpectrumContract : ImpactContract
    {

        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;
            foreach (CelestialBody body in bodies)
            {
                IEnumerable<SeismicContract> contracts = ContractSystem.Instance.GetCurrentActiveContracts<SeismicContract>().Where(contract => contract.targetBody == body);
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given body at once
                //I guess you could have differnt biomes though //TODO
                ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");

                ScienceSubject subject;
                ExperimentSituations sit = ExperimentSituations.SrfLanded;
                subject = ResearchAndDevelopment.GetExperimentSubject(experiment, sit, body, "surface");
                int stars = starRatings[prestige];
                string biome = pickKE(stars, subject, body);
                possible.Add(new PossibleContract(1,  body, energy));
            }
            return possible;
        }

        private List<ContractPrestige> getAvailableStars(ScienceSubject subject)
        {
            int acheived = (int)Math.Max(subject.scientificValue / subject.scienceCap * 4, 3);
            List<ContractPrestige> pres = new List<ContractPrestige>() { ContractPrestige.Trivial, ContractPrestige.Significant, ContractPrestige.Exceptional };
            for (int i = 0; i < acheived; i++)
            {
                pres.RemoveAt(0);
            }
            return pres;
        }

        protected override string GetTitle()
        {
            return "Record an impact with a spectrometer in the " + pickedContract.biome + " of " + pickedContract.body.name;
        }
    }*/

    class ImpactParameter : ContractParameter
    {
        private const string keTitle = "Crash into {0} with {1}";
        private const string biomeTitle = "Crash into {0} on {1}";

        ImpactContract.PossibleContract contract;

        public ImpactParameter()
        {

        }
        
        public ImpactParameter(ImpactContract.PossibleContract contract) {
            this.contract = contract;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            ImpactMonitor.Log("adding bang listener");
            ImpactCoordinator.getInstance().bangListeners.Add(OnBang);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
        }

        private void OnBang(ImpactScienceData data)
        {
            ImpactMonitor.Log("bang received");
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(data.subjectID);
            if (data.kineticEnergy >= contract.energy && subject.IsFromBody(contract.body))
            {
                //if a biome is specified  then check the biome matches
                if (contract.biome != null && subject.IsFromSituation(ExperimentSituations.InSpaceLow) && data.biome == contract.biome)
                {
                    SetComplete();
                }
                else
                {
                    SetComplete();
                }
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
            if (contract.biome == null)
                return String.Format(keTitle, contract.body.theName, ImpactMonitor.energyFormat(contract.energy));
            else
                return String.Format(biomeTitle, contract.biome, contract.body.theName);
        }
    }

    class ScienceReceiptParameter : ContractParameter
    {
        private const string keTitle = "Recover science data";

        ImpactContract.PossibleContract contract;

        public ScienceReceiptParameter()
        {

        }

        public ScienceReceiptParameter(ImpactContract.PossibleContract contract)
        {
            
            this.contract = contract;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            ImpactMonitor.Log("adding science receipt listener");
            ImpactCoordinator.getInstance().scienceListeners.Add(OnScience);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            ImpactCoordinator.getInstance().bangListeners.Remove(OnScience);
        }

        private void OnScience(ImpactScienceData data)
        {
            ImpactMonitor.Log("science received");
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(data.subjectID);
            if (data.kineticEnergy >= contract.energy && subject.IsFromBody(contract.body))
            {
                //if a biome is specified  then check the biome matches
                if (contract.biome != null && subject.IsFromSituation(ExperimentSituations.InSpaceLow) && data.biome == contract.biome)
                {
                    SetComplete();
                }
                else
                {
                    SetComplete();
                }
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

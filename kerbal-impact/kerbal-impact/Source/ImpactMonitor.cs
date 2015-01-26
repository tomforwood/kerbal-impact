using KSP.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace kerbal_impact
{
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, true)]
    public class ImpactMonitor : MonoBehaviour
    {

        private static ImpactMonitor instance;

        
        public ImpactMonitor()
        {
            Log("ImpactMonitorConstructor");
            if (instance == null)
            {
                Log("Settings instance in constructor");
                instance = this;
            }
        }

        public static ImpactMonitor getInstance()
        {
            if (instance == null)
            {
                new ImpactMonitor();
                Log("Starting from getInsance");
            }
            Log(" get instance instance id " + instance.GetInstanceID());
            return instance;
        }
        
        
        public static void Log(string message)
        {
            Debug.Log("[" + Time.time.ToString("0.00") + "]: " + message);
        }

        void Start()
        {
            if (this != instance) return;//only start one instance
            Log("Its starting");
            //GameEvents.onPartDie.Add(OnPartDie);
            GameEvents.onCrash.Add(OnCrash);
            GameEvents.OnVesselRecoveryRequested.Add(OnVesselRecovered);
        }

        void Stop()
        {
            GameEvents.onCrash.Remove(OnCrash);
        }

        private void OnVesselRecovered(Vessel vessel) 
        {
            List<Seismometer> seismographs = vessel.FindPartModulesImplementing<Seismometer>();
            IEnumerable<ImpactScienceData> sciences = seismographs.SelectMany(s => s.GetImpactData());
            //TODO add spectrograph data to this too
            foreach (ImpactScienceData science in sciences) {
                scienceToKSC(science);
            }
        }

        public void scienceToKSC(ImpactScienceData data)
        {
            ImpactCoordinator.getInstance().scienceListeners.Fire(data);
        }

        

        private void OnCrash(EventReport report)
        {
            Part crashPart = report.origin;

            Log("crash data "+report.other+"- " + report.sender +"-"+ crashPart.vessel+"-" + crashPart.vessel.srf_velocity.magnitude);
            Vessel crashVessel= crashPart.vessel;
            CelestialBody crashBody = crashVessel.orbit.referenceBody;
            if (crashBody.atmosphere) return;
            Log("Crashed on "+crashBody.theName);
            //find all craft orbiting and landed at this body
            foreach (Vessel vessel in FlightGlobals.Vessels.Where(v=>v.orbit.referenceBody==crashBody)) {
                Log("Found a vessel around");
                if (vessel.situation==Vessel.Situations.LANDED) {
                    landedVessel(crashBody, vessel, crashVessel);
                }
            }
        }

        private void landedVessel(CelestialBody crashBody, Vessel vessel, Vessel crashVessel)
        {
            Log("And it is landed");
            if (vessel.loaded)
            {
                List<Seismometer> seismographs = vessel.FindPartModulesImplementing<Seismometer>();
                if (seismographs.Count != 0)
                {
                    Log("Found seismographs");
                    Log("in crashstuff instanceid= " + instance.GetInstanceID());
                    ImpactScienceData data = createSeismologyData(crashBody, crashVessel);
                    ImpactCoordinator.getInstance().bangListeners.Fire(data);
                    seismographs[0].addExperiment(data);

                }
            }
            else
            {
                List<ProtoPartSnapshot> parts = vessel.protoVessel.protoPartSnapshots;
                foreach (ProtoPartSnapshot snap in parts)
                {
                    foreach (ProtoPartModuleSnapshot mod in snap.modules)
                    {
                        //TODO only update 1 seismometer
                        if (mod.moduleName == "Seismometer")
                        {
                            Log("Found seismographs");
                            Log("in crashstuff instanceid= " + instance.GetInstanceID());
                            ImpactScienceData data = createSeismologyData(crashBody, crashVessel);
                            ImpactCoordinator.getInstance().bangListeners.Fire(data);
                            Seismometer.NewResult(mod.moduleValues, data);
                        }
                    }
                }
            }
        }

        private static ImpactScienceData createSeismologyData(CelestialBody crashBody, Vessel crashVessel)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            Log("Mass=" + crashMasss);
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash
            Log("Energy=" + crashEnergy);


            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.SrfLanded, crashBody, "surface");
            double science = translateKEToScience(crashEnergy, crashBody, subject);

            String flavourText = "Impact of";

            science = Math.Max(0, science - subject.science);
            science /= subject.subjectValue;

            ImpactScienceData data = new ImpactScienceData((float)crashEnergy, (float)(science * subject.dataScale), 1f, 0, subject.id, flavourText + energyFormat(crashEnergy));

            return data;
        }

        public static double translateScienceToKE(double science, CelestialBody crashBody, ScienceSubject subject)
        {
            double referenceEnergy = getReferenceCrash(crashBody);
            Log("ReferenceCrash=" + referenceEnergy);
            double relativeScience = science / subject.scienceCap;
            Log("Science=" + science + " relative = " + relativeScience);
            double crashEnergy = relativeScience * relativeScience * referenceEnergy;
            Log("crashEnergy=" + crashEnergy);
            return crashEnergy;
        }

        public static double translateKEToScience(double crashEnergy, CelestialBody crashBody, ScienceSubject subject)
        {
            double referenceEnergy = getReferenceCrash(crashBody);

            float relativeScience = Math.Min((float)(Math.Sqrt(crashEnergy / referenceEnergy)), 1);
            return relativeScience * subject.scienceCap;
        }

        public static double getReferenceCrash(CelestialBody crashBody)
        {
            //Science amount is relative to a 15 tonne impactor at escape velocity
            double mu = crashBody.gravParameter;
            double radius = crashBody.Radius;
            double referenceEnergy = 15e3 * mu / radius;
            return referenceEnergy;
        }

        private static string[] suffixes = {"J","kJ","MJ","GJ","TJ","PJ"};
        public static string energyFormat(double crashEnergy)
        {
            int suffixIndex = 0;
            double energyFigs = crashEnergy;

            while (energyFigs >= 1000 && suffixIndex<suffixes.Count())
            {
                energyFigs /= 1000;
                suffixIndex++;
            }

            //There must be a nice way to show exactly 3 sig figs right?
            string sigFigFormat;
            if (energyFigs >= 100) sigFigFormat = "000";
            else if (energyFigs >= 10) sigFigFormat = "00.0";
            else sigFigFormat = "0.00";
            string result = String.Format("{0:"+sigFigFormat+"}{1}", energyFigs, suffixes[suffixIndex]);
            Log(result);
            return result;
        }

        
    }
}
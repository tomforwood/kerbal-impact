using KSP.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace kerbal_impact
{
    
    public class ImpactMonitor 
    {

        private static ImpactMonitor instance;

        
        private ImpactMonitor()
        {
        }

        public static ImpactMonitor getInstance()
        {
            if (instance == null)
            {
                instance = new ImpactMonitor();
                Log("Starting from getInsance");
            }
            return instance;
        }
        
        
        public static void Log(string message)
        {
            Debug.Log("[IM:" + Time.time.ToString("0.00") + "]: " + message);
        }

        public void Start()
        {
            Log("Its starting");
            GameEvents.onCrash.Add(OnCrash);
            GameEvents.onCollision.Add(OnCollide);
            GameEvents.OnVesselRecoveryRequested.Add(OnVesselRecovered);
            //listBiones(Planetarium.fetch.Sun);
        }

        private void listBiones(CelestialBody body)
        {
            //todo temporary
            Log("attname=" + body.bodyName);
            CBAttributeMapSO m = body.BiomeMap;
            CBAttributeMapSO.MapAttribute[] atts = m.Attributes;
            foreach (CBAttributeMapSO.MapAttribute att in atts)
            {
                Log("att=" + att.name + "-" + att.value);
            }
            foreach (CelestialBody sub in body.orbitingBodies)
            {
                listBiones(sub);
            }
        }

        public void Stop()
        {
            GameEvents.onCrash.Remove(OnCrash);
            GameEvents.onCollision.Remove(OnCollide);
            GameEvents.OnVesselRecoveryRequested.Remove(OnVesselRecovered);
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
            if (crashPart.vessel.srf_velocity.magnitude<50) return;
            Log("crash data " + report.msg + "-" + report.eventType + "-" + report.other + "- " + report.sender + "-" + crashPart.vessel + "-" + crashPart.vessel.srf_velocity.magnitude);
            Vessel crashVessel= crashPart.vessel;
            doImpact(crashVessel);
        }

        private void OnCollide(EventReport report)
        {
            Part crashPart = report.origin;
            if (crashPart.vessel.srf_velocity.magnitude <50) return;
            if (report.other != "the surface") return;
            Log("collide data " +report.msg+ "-" +report.eventType+"-"+ report.other + "- " + report.sender + "-" + crashPart.vessel + "-" + crashPart.vessel.srf_velocity.magnitude);
            Vessel crashVessel = crashPart.vessel;
            doImpact(crashVessel);
        }

        private void doImpact(Vessel crashVessel) {
            CelestialBody crashBody = crashVessel.orbit.referenceBody;
            if (crashBody.atmosphere) return;
            Log("Crashed on "+crashBody.theName);
            //find all craft orbiting and landed at this body
            foreach (Vessel vessel in FlightGlobals.Vessels.Where(v=>v.orbit.referenceBody==crashBody)) {
                Log("Found a vessel around");
                if (vessel.situation==Vessel.Situations.LANDED) {
                    landedVessel(crashBody, vessel, crashVessel);
                }
                if (vessel.situation == Vessel.Situations.ORBITING)
                {
                    orbitingVessel(crashBody, vessel, crashVessel);
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
                    ImpactScienceData data = createSeismicData(crashBody, crashVessel);
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
                        if (mod.moduleName == "Seismometer")
                        {
                            Log("Found seismographs");
                            ImpactScienceData data = createSeismicData(crashBody, crashVessel);
                            ImpactCoordinator.getInstance().bangListeners.Fire(data);
                            Seismometer.NewResult(mod.moduleValues, data);
                            return;
                        }
                    }
                }
            }
        }

        private void orbitingVessel(CelestialBody crashBody, Vessel vessel, Vessel crashVessel)
        {
            Log("And it is orbiting");
            Log("CelestialBody is at " + crashBody.position);
            Log("Crash vessel is at" + crashVessel.CoM);
            Log("Orbiter is at" + vessel.CoM);
            Vector3d crash = crashVessel.CoM;
            crash = crashVessel.CoM - crashBody.position;
            Log("crashRelaticeTocentre =" + crash);
            Vector3d orbVec = vessel.CoM - crashBody.position;
            Vector3d sightVec = (orbVec-crash);
            double angle = Vector3d.Angle(crash, sightVec);
            Log("Sight=" + sightVec);
            Log("sight angle = " + angle +" degrees");
            Log("Distance between themn =" + (crash - orbVec).magnitude);

            if (angle < 90)
            {
                Log("Vessel is visible");
                if (vessel.loaded)
                {
                    List<Spectrometer> spectrometers = vessel.FindPartModulesImplementing<Spectrometer>();
                    if (spectrometers.Count != 0)
                    {
                        Log("Found spectrometers");
                        ImpactScienceData data = createSpectralData(crashBody, crashVessel);
                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                        spectrometers[0].addExperiment(data);

                    }
                }
                else
                {
                    List<ProtoPartSnapshot> parts = vessel.protoVessel.protoPartSnapshots;
                    foreach (ProtoPartSnapshot snap in parts)
                    {
                        foreach (ProtoPartModuleSnapshot mod in snap.modules)
                        {
                            if (mod.moduleName == "Spectrometer")
                            {
                                Log("Found spectrometers");
                                ImpactScienceData data = createSpectralData(crashBody, crashVessel);
                                Log("about to call listeners");
                                ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                Log("About to call newresult");
                                Spectrometer.NewResult(mod.moduleValues, data);
                                return;
                            }
                        }
                    }
                }
            }
        }

        private static ImpactScienceData createSeismicData(CelestialBody crashBody, Vessel crashVessel)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash


            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.SrfLanded, crashBody, "");
            double science = translateKEToScience(crashEnergy, crashBody, subject);

            String flavourText = "Impact of {0} on {1}";
            Log(" caluculated science =" + science);
            science = Math.Max(0.01, science - subject.science);
            Log("residual science =" + science);
            
            science /= subject.subjectValue;
            Log("divided science =" + science);

            ImpactScienceData data = new ImpactScienceData((float)crashEnergy, null, crashVessel.latitude,
                (float)(science * subject.dataScale), 1f, 0, subject.id, 
                String.Format(flavourText, energyFormat(crashEnergy), crashBody.theName));

            ScreenMessages.PostScreenMessage(
                String.Format("Recoreded seismic impact of {0} on {1}",energyFormat(crashEnergy), crashBody.theName),
                5.0f, ScreenMessageStyle.UPPER_RIGHT);


            return data;
        }

        private static ImpactScienceData createSpectralData(CelestialBody crashBody, Vessel crashVessel)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSpectrometer");
            String biome = ScienceUtil.GetExperimentBiome(crashBody, crashVessel.latitude, crashVessel.longitude);
            CBAttributeMapSO m = crashBody.BiomeMap;
            CBAttributeMapSO.MapAttribute[] atts = m.Attributes;
            foreach (CBAttributeMapSO.MapAttribute att in atts)
            {
                Log("att=" + att.name+"-"+att.value);
            }
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceLow, crashBody, biome);
            double science = subject.scienceCap;
            Log("Impact took place in " + biome + " at " + crashVessel.latitude + "," + crashVessel.longitude);
            String flavourText = "Impact at {0} on {1}";

            science = Math.Max(0, science - subject.science);
            science /= subject.subjectValue;

            ImpactScienceData data = new ImpactScienceData(0, biome, crashVessel.latitude,
                (float)(science * subject.dataScale), 1f, 0, subject.id, 
                String.Format(flavourText, biome, crashBody.theName));

            ScreenMessages.PostScreenMessage(
                String.Format("Recoreded spectrographic impact data at {0} on {1}", biome, crashBody.theName),
                5.0f, ScreenMessageStyle.UPPER_RIGHT);

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
            return result;
        }

        
    }
}
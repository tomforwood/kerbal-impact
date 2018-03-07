using KSP.IO;
using KSP.Localization;
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
            Debug.Log("[IM:" + DateTime.Now + "]: " + message);
        }

        public void Start()
        {
            Log("Its starting");
            GameEvents.onCrash.Add(OnCrash);
            GameEvents.onCollision.Add(OnCollide);
            GameEvents.OnVesselRecoveryRequested.Add(OnVesselRecovered);
            
            //listBiones(Planetarium.fetch.Sun);
        }

        private void listBiomes(CelestialBody body)
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
                listBiomes(sub);
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
            doImpact(crashVessel, null);
        }

        private void OnCollide(EventReport report)
        {
            Part crashPart = report.origin;
            Log("Something crashed into something" + crashPart+ "->" + report.other);
            if (crashPart.vessel.srf_velocity.magnitude <50) return;
            Vessel asteroid=null;
            foreach (Vessel v in FlightGlobals.Vessels) {
                Log(v.vesselName + " " +v.vesselType + " " + v.RevealName());
                if (v.vesselName == report.other && v.vesselType == VesselType.SpaceObject)
                {
                    asteroid = v;
                    break;
                }
            }
            if (report.other != "the surface" && asteroid==null ) return;
            Log("collide data " +report.msg+ "-" +report.eventType+"-"+ report.other + "- " + report.sender + "-" + crashPart.vessel + "-" + crashPart.vessel.srf_velocity.magnitude);
            Vessel crashVessel = crashPart.vessel;
            doImpact(crashVessel, asteroid);
        }

        private void doImpact(Vessel crashVessel, Vessel asteroid) {
            CelestialBody crashBody = crashVessel.orbit.referenceBody;
            if (crashBody.atmosphere && asteroid==null) return;
            Log("Crashed on "+crashBody.name);
            //find all craft orbiting and landed at this body
            foreach (Vessel vessel in FlightGlobals.Vessels.Where(v=>v.orbit.referenceBody==crashBody)) {
                Log("Found a vessel "+vessel.GetName());
                if (asteroid==null) {
                    if (vessel.situation==Vessel.Situations.LANDED) {
                        landedVessel(crashBody, vessel, crashVessel);
                    }
                    if (vessel.situation == Vessel.Situations.ORBITING)
                    {
                        orbitingVessel(crashBody, vessel, crashVessel);
                    }
                }
                else
                {
                    if (vessel.situation == Vessel.Situations.ORBITING)
                    {
                        nearAsteroidVessel(vessel, crashVessel, asteroid, crashBody);
                    }
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
					ImpactScienceData data = createSeismicData(crashBody, crashVessel, seismographs[0].part.flightID);
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
							ImpactScienceData data = createSeismicData(crashBody, crashVessel,snap.flightID);
                            ImpactCoordinator.getInstance().bangListeners.Fire(data);
                            Seismometer.NewResult(mod.moduleValues, data);
                            return;
                        }
                    }
                }
            }
        }

        private void nearAsteroidVessel(Vessel observer, Vessel crashVessel, Vessel asteroid, CelestialBody crashBody)
        {
            Log("observer is orbiting ");
            Log("observer is at " + observer.CoM);
            Log("Crash vessel is at" + crashVessel.CoM);
            Vector3d sightVec = observer.CoM - crashVessel.CoM;
            Log("Distance between them =" + sightVec.magnitude);


            if (sightVec.magnitude < 5e5)
            {
                //observer is in range (500km)
                Log("It is in range =" + (sightVec).magnitude);
                if (observer.loaded)
                {
                    List<Spectrometer> spectrometers = observer.FindPartModulesImplementing<Spectrometer>();
                    if (spectrometers.Count != 0)
                    {
                        Log("Found loaded spectrometers");
						ImpactScienceData data = createAsteroidSpectralData(crashBody, asteroid, crashVessel, spectrometers[0].part.flightID);
                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                        spectrometers[0].addExperiment(data);

                    }
                }
                else
                {
                    List<ProtoPartSnapshot> parts = observer.protoVessel.protoPartSnapshots;
                    foreach (ProtoPartSnapshot snap in parts)
                    {
                        foreach (ProtoPartModuleSnapshot mod in snap.modules)
                        {
                            if (mod.moduleName == "Spectrometer")
                            {
                                Log("Found unloaded spectrometers");
								ImpactScienceData data = createAsteroidSpectralData(crashBody, asteroid, crashVessel, snap.flightID);
                                ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                Spectrometer.NewResult(mod.moduleValues, data);
                                return;
                            }
                        }
                    }
                }
            }

        }

        private void orbitingVessel(CelestialBody crashBody, Vessel observer, Vessel crashVessel)
        {
            Log("And it is orbiting");
            Log("CelestialBody is at " + crashBody.position);
            Log("Crash vessel is at" + crashVessel.CoM);
            Log("Observer is at" + observer.CoM);
            Vector3d crash = crashVessel.CoM;
            crash = crashVessel.CoM - crashBody.position;
            Log("crashRelaticeTocentre =" + crash);
            Vector3d orbVec = observer.CoM - crashBody.position;
            Vector3d sightVec = (orbVec-crash);
            double angle = Vector3d.Angle(crash, sightVec);
            Log("Sight=" + sightVec);
            Log("sight angle = " + angle +" degrees");
            Log("Distance between them =" + sightVec.magnitude);

            if (angle < 90)
            {
                Log("Vessel is visible");
                if (observer.loaded)
                {
                    List<Spectrometer> spectrometers = observer.FindPartModulesImplementing<Spectrometer>();
                    if (spectrometers.Count != 0)
                    {
                        Log("Found spectrometers");
						ImpactScienceData data = createSpectralData(crashBody, crashVessel, spectrometers[0].part.flightID);
                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                        spectrometers[0].addExperiment(data);

                    }
                }
                else
                {
                    List<ProtoPartSnapshot> parts = observer.protoVessel.protoPartSnapshots;
                    foreach (ProtoPartSnapshot snap in parts)
                    {
                        foreach (ProtoPartModuleSnapshot mod in snap.modules)
                        {
                            if (mod.moduleName == "Spectrometer")
                            {
                                Log("Found spectrometers");
								ImpactScienceData data = createSpectralData(crashBody, crashVessel, snap.flightID);
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

		private static ImpactScienceData createSeismicData(CelestialBody crashBody, Vessel crashVessel, uint flightID)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash


            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.SrfLanded, crashBody, "", "");
            double science = translateKEToScience(crashEnergy, crashBody, subject);

            String flavourText = "Impact of <<1>> on <<2>>";
            Log(" caluculated science =" + science);
            science = Math.Max(0.01, science - subject.science);
            Log("residual science =" + science);
            
            science /= subject.subjectValue;
            Log("divided science =" + science);

            ImpactScienceData data = new ImpactScienceData(ImpactScienceData.DataTypes.Seismic, 
                (float)crashEnergy, null, crashVessel.latitude,
                (float)(science * subject.dataScale), 1f, 0, subject.id,
                Localizer.Format(flavourText, energyFormat(crashEnergy), crashBody.GetDisplayName()), false, flightID);

            ScreenMessages.PostScreenMessage(
                Localizer.Format("#autoLOC_Screen_Seismic", energyFormat(crashEnergy), crashBody.GetDisplayName()),
                5.0f, ScreenMessageStyle.UPPER_RIGHT);


            return data;
        }

		private static ImpactScienceData createSpectralData(CelestialBody crashBody, Vessel crashVessel, uint flightID)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSpectrometer");
            String biome = ScienceUtil.GetExperimentBiome(crashBody, crashVessel.latitude, crashVessel.longitude);
            CBAttributeMapSO m = crashBody.BiomeMap;
            CBAttributeMapSO.MapAttribute[] atts = m.Attributes;
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceLow, crashBody, biome, biome);
            double science = subject.scienceCap;
            Log("Impact took place in " + biome + " at " + crashVessel.latitude + "," + crashVessel.longitude);
            String flavourText = "Impact at <<1>> on <<2>>";

            science = Math.Max(0, science - subject.science);
            science /= subject.subjectValue;

            ImpactScienceData data = new ImpactScienceData(ImpactScienceData.DataTypes.Spectral,
                0, biome, crashVessel.latitude,
                (float)(science * subject.dataScale), 1f, 0, subject.id, 
				Localizer.Format(flavourText, biome, crashBody.GetDisplayName()), false, flightID);

            ScreenMessages.PostScreenMessage(
                Localizer.Format("#autoLOC_Screen_Spectrum", biome, crashBody.GetDisplayName()),
                5.0f, ScreenMessageStyle.UPPER_RIGHT);

            return data;
        }

		private static ImpactScienceData createAsteroidSpectralData(CelestialBody crashBody, Vessel asteroid, Vessel crashVessel, uint flightID)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("AsteroidSpectometry");
            ExperimentSituations situation = ScienceUtil.GetExperimentSituation(asteroid);

            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, asteroid.id.ToString(), asteroid.GetName(), crashBody, "", "");
            double science = subject.scienceCap;
            Log("Impact took place in " + situation);
            String flavourText = "Impact at <<1>> on <<2>>";

  
            science /= subject.subjectValue;

            ImpactScienceData data = new ImpactScienceData(0, asteroid.GetName(), 
                (float)(science * subject.dataScale), 1f, 0, subject.id,
                Localizer.Format(flavourText, asteroid.GetName(), crashBody.GetDisplayName()), false, flightID);

            ScreenMessages.PostScreenMessage(
                Localizer.Format("#autoLOC_Screen_Asteroid", asteroid.GetName(), crashBody.GetDisplayName()),
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
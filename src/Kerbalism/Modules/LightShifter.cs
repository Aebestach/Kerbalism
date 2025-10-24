using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KERBALISM.Modules
{
	// Class that manages the light of stars
	public class LightShifter : MonoBehaviour
	{
		// Variables
		public Color sunlightColor;
		public Single sunlightIntensity;
		public Single sunlightShadowStrength;
		public Color scaledSunlightColor;
		public Single scaledSunlightIntensity;
		public Color ivaSunColor;
		public Single ivaSunIntensity;
		public Color ambientLightColor;
		public Color sunLensFlareColor;
		public Boolean givesOffLight;
		public Double au;
		public FloatCurve brightnessCurve;
		public FloatCurve intensityCurve;
		public FloatCurve scaledIntensityCurve;
		public FloatCurve ivaIntensityCurve;
		public Double solarInsolation;
		public Double solarLuminosity;
		public Double radiationFactor;
		public Flare sunFlare;

		// Prefab that makes every star yellow by default
		public static LightShifter Prefab
		{
			get
			{
				// If the prefab is null, create it
				GameObject prefabGob = new GameObject("LightShifter");
				LightShifter prefab = prefabGob.AddComponent<LightShifter>();

				// Fill it with default values
				prefab.sunlightColor = Color.white;
				prefab.sunlightIntensity = 0.9f;
				prefab.sunlightShadowStrength = 0.7523364f;
				prefab.scaledSunlightColor = Color.white;
				prefab.scaledSunlightIntensity = 0.9f;
				prefab.ivaSunColor = new Color(1.0f, 0.977f, 0.896f, 1.0f);
				prefab.ivaSunIntensity = 0.8099999f;
				prefab.sunLensFlareColor = Color.white;
				prefab.ambientLightColor = new Color(0.06f, 0.06f, 0.06f, 1.0f);
				prefab.au = 13599840256;
				prefab.brightnessCurve = new FloatCurve(new[]
				{
					new Keyframe(-0.01573471f, 0.217353f, 1.706627f, 1.706627f), new Keyframe(5.084181f, 3.997075f, -0.001802375f, -0.001802375f), new Keyframe(38.56295f, 1.82142f, 0.0001713f, 0.0001713f)
				});
				prefab.givesOffLight = true;
				prefab.solarInsolation = PhysicsGlobals.SolarInsolationAtHome;
				prefab.solarLuminosity = PhysicsGlobals.SolarLuminosityAtHome;
				prefab.radiationFactor = PhysicsGlobals.RadiationFactor;
				prefab.sunFlare = null;
				prefab.intensityCurve = new FloatCurve(new[]
				{
					new Keyframe(0, prefab.sunlightIntensity), new Keyframe(1, prefab.sunlightIntensity)
				});
				prefab.scaledIntensityCurve = new FloatCurve(new[]
				{
					new Keyframe(0, prefab.scaledSunlightIntensity), new Keyframe(1, prefab.scaledSunlightIntensity)
				});
				prefab.ivaIntensityCurve = new FloatCurve(new[]
				{
					new Keyframe(0, prefab.ivaSunIntensity), new Keyframe(1, prefab.ivaSunIntensity)
				});

				// Return the prefab
				return prefab;
			}
		}

		/// <summary>
		/// Applies the ambient color.
		/// </summary>
		public void ApplyAmbient()
		{
			DynamicAmbientLight ambientLight = FindObjectOfType(typeof(DynamicAmbientLight)) as DynamicAmbientLight;
			if (ambientLight)
			{
				ambientLight.vacuumAmbientColor = ambientLightColor;
			}
		}

		public static Double SolarIntensityAtHomeMultiplier = 0;

		public void ApplyPhysics()
		{
			if (!FlightGlobals.ready)
			{
				return;
			}

			if (SolarIntensityAtHomeMultiplier == 0)
			{
				CelestialBody homeBody = FlightGlobals.GetHomeBody();

				if (homeBody == null)
				{
					return;
				}

				while (KopernicusStar.Stars.All(s => s.sun != homeBody.referenceBody) && homeBody.referenceBody != null)
				{
					homeBody = homeBody.referenceBody;
				}

				SolarIntensityAtHomeMultiplier = Math.Pow(homeBody.orbit.semiMajorAxis, 2) * 4 * 3.14159265358979;
			}

			if (solarLuminosity > 0)
			{
				PhysicsGlobals.SolarLuminosityAtHome = solarLuminosity;
			}
			else
			{
				PhysicsGlobals.SolarLuminosityAtHome = 0.0000000000001; //near zero value to not freak out MFI.
			}
			PhysicsGlobals.SolarInsolationAtHome = solarInsolation;
			PhysicsGlobals.RadiationFactor = radiationFactor;

			FieldInfo SolarLuminosity = typeof(PhysicsGlobals).GetField("solarLuminosity", BindingFlags.NonPublic | BindingFlags.Instance);
			SolarLuminosity.SetValue(PhysicsGlobals.Instance, SolarIntensityAtHomeMultiplier * PhysicsGlobals.SolarLuminosityAtHome);
		}
	}
}

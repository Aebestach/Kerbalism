using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// A broken Reliability or EngineFailures component on a loaded or unloaded vessel.
	/// Used by career repair contracts and by other mods via <see cref="API.GetBrokenComponents"/>.
	/// </summary>
	public class BrokenComponent
	{
		public const string ModuleReliability = "Reliability";
		public const string ModuleEngineFailures = "EngineFailures";

		public Vessel vessel;
		public uint vesselPersistentId;
		public uint partFlightId;
		public string partName;
		public string partTitle;
		public string moduleName;
		public string type;
		public string title;
		public bool critical;
		public bool crewed;
		public CelestialBody body;

		/// <summary>
		/// Extra scanners (EngineFailures registers here). Called after the core Reliability scan
		/// for each simulated vessel.
		/// </summary>
		public static readonly List<Action<Vessel, List<BrokenComponent>>> ExtraProviders =
			new List<Action<Vessel, List<BrokenComponent>>>();

		public static List<BrokenComponent> CollectAll()
		{
			List<BrokenComponent> result = new List<BrokenComponent>();
			if (FlightGlobals.Vessels == null)
				return result;

			foreach (Vessel v in FlightGlobals.Vessels)
			{
				if (v == null || v.isEVA || !Lib.IsVessel(v))
					continue;
				if (!v.KerbalismData().IsSimulated)
					continue;
				Collect(v, result);
			}

			return result;
		}

		public static void Collect(Vessel v, List<BrokenComponent> result)
		{
			if (v == null)
				return;

			if (v.loaded)
				CollectLoaded(v, result);
			else
				CollectProto(v, result);

			foreach (Action<Vessel, List<BrokenComponent>> provider in ExtraProviders)
			{
				try
				{
					provider(v, result);
				}
				catch (Exception e)
				{
					Lib.Log("BrokenComponent extra provider: " + e.Message + "\n" + e);
				}
			}
		}

		/// <summary>
		/// Find the current vessel that owns the part and whether that specific module is still broken.
		/// Returns false if the part no longer exists (destroyed, recovered, or not yet loaded).
		/// </summary>
		public static bool TryGetState(uint partFlightId, string moduleName, string type, out Vessel vessel, out bool broken)
		{
			vessel = null;
			broken = false;
			if (partFlightId == 0 || FlightGlobals.Vessels == null)
				return false;

			foreach (Vessel v in FlightGlobals.Vessels)
			{
				if (v == null)
					continue;

				if (v.loaded)
				{
					for (int i = 0; i < v.parts.Count; i++)
					{
						Part p = v.parts[i];
						if (p.flightID != partFlightId)
							continue;
						vessel = v;
						broken = IsLoadedBroken(p, moduleName, type);
						return true;
					}
				}
				else if (v.protoVessel != null)
				{
					List<ProtoPartSnapshot> parts = v.protoVessel.protoPartSnapshots;
					for (int i = 0; i < parts.Count; i++)
					{
						ProtoPartSnapshot p = parts[i];
						if (p.flightID != partFlightId)
							continue;
						vessel = v;
						broken = IsProtoBroken(p, moduleName, type);
						return true;
					}
				}
			}

			return false;
		}

		public static BrokenComponent FromLoaded(Vessel v, Part part, string moduleName, string type, string title, bool critical)
		{
			VesselData vd = v.KerbalismData();
			string partTitle = part.partInfo != null ? part.partInfo.title : part.partName;
			return new BrokenComponent
			{
				vessel = v,
				vesselPersistentId = v.persistentId,
				partFlightId = part.flightID,
				partName = part.partInfo != null ? part.partInfo.name : part.partName,
				partTitle = partTitle,
				moduleName = moduleName,
				type = type ?? string.Empty,
				title = title ?? string.Empty,
				critical = critical,
				crewed = vd.CrewCount > 0,
				body = v.mainBody
			};
		}

		public static BrokenComponent FromProto(Vessel v, ProtoPartSnapshot part, string moduleName, string type, string title, bool critical)
		{
			VesselData vd = v.KerbalismData();
			string partTitle = part.partInfo != null ? part.partInfo.title : part.partName;
			return new BrokenComponent
			{
				vessel = v,
				vesselPersistentId = v.persistentId,
				partFlightId = part.flightID,
				partName = part.partName,
				partTitle = partTitle,
				moduleName = moduleName,
				type = type ?? string.Empty,
				title = title ?? string.Empty,
				critical = critical,
				crewed = vd.CrewCount > 0,
				body = v.mainBody
			};
		}

		static void CollectLoaded(Vessel v, List<BrokenComponent> result)
		{
			foreach (Reliability r in PartModuleCache.GetModules<Reliability>(v))
			{
				if (!r.isEnabled || !r.broken)
					continue;
				result.Add(FromLoaded(v, r.part, ModuleReliability, r.type, Reliability.LocalizeTitle(r.title), r.critical));
			}
		}

		static void CollectProto(Vessel v, List<BrokenComponent> result)
		{
			if (v.protoVessel == null)
				return;

			Dictionary<string, Lib.Module_prefab_data> prefabData = new Dictionary<string, Lib.Module_prefab_data>();
			foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
			{
				AvailablePart partInfo = PartLoader.getPartInfoByName(p.partName);
				if (partInfo == null || partInfo.partPrefab == null)
					continue;

				prefabData.Clear();
				foreach (ProtoPartModuleSnapshot m in p.modules)
				{
					if (m.moduleName != ModuleReliability)
						continue;

					Reliability prefab = Lib.ModulePrefab(partInfo.partPrefab.Modules, m.moduleName, prefabData) as Reliability;
					if (prefab == null)
						continue;
					if (!Lib.Proto.GetBool(m, "isEnabled"))
						continue;
					if (!Lib.Proto.GetBool(m, "broken"))
						continue;

					string type = Lib.Proto.GetString(m, "type", prefab.type);
					result.Add(FromProto(
						v,
						p,
						ModuleReliability,
						type,
						Reliability.LocalizeTitle(prefab.title),
						Lib.Proto.GetBool(m, "critical")));
				}
			}
		}

		static bool IsLoadedBroken(Part part, string moduleName, string type)
		{
			if (moduleName == ModuleReliability)
			{
				foreach (Reliability r in part.FindModulesImplementing<Reliability>())
				{
					if (r.type == type)
						return r.broken;
				}
				return false;
			}

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule m = part.Modules[i];
				if (m == null || m.moduleName != moduleName)
					continue;
				BaseField field = m.Fields["broken"];
				if (field == null)
					return false;
				object value = field.GetValue(m);
				return value is bool b && b;
			}
			return false;
		}

		static bool IsProtoBroken(ProtoPartSnapshot part, string moduleName, string type)
		{
			foreach (ProtoPartModuleSnapshot m in part.modules)
			{
				if (m.moduleName != moduleName)
					continue;
				if (moduleName == ModuleReliability)
				{
					if (Lib.Proto.GetString(m, "type") != type)
						continue;
				}
				return Lib.Proto.GetBool(m, "broken");
			}
			return false;
		}
	}
}

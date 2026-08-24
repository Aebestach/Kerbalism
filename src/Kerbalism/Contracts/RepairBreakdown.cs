using System;
using System.Collections.Generic;
using Contracts;
using UnityEngine;

namespace KERBALISM.CONTRACTS
{
	// Repair a specific Kerbalism reliability / engine-failure breakdown (#1194)
	public sealed class RepairBreakdown : Contract
	{
		internal uint vesselPersistentId;
		internal uint partFlightId;
		internal string moduleName = string.Empty;
		internal string type = string.Empty;
		internal string vesselName = string.Empty;
		internal string componentTitle = string.Empty;
		internal string partTitle = string.Empty;
		internal string bodyName = string.Empty;
		internal bool crewed;
		internal bool critical;

		float missingRealtime;

		internal string LiveVesselName()
		{
			if (BrokenComponent.TryGetState(partFlightId, moduleName, type, out Vessel v, out _))
				return v.vesselName;
			return vesselName;
		}

		internal static string TargetKey(uint partFlightId, string moduleName, string type)
		{
			return partFlightId + "/" + moduleName + "/" + type;
		}

		protected override bool Generate()
		{
			if (TooManyExisting())
				return false;

			List<BrokenComponent> candidates = EligibleCandidates();
			if (candidates.Count == 0)
				return false;

			BrokenComponent chosen = candidates[Lib.RandomInt(candidates.Count)];
			Assign(chosen);

			SetExpiry(15f, 45f);
			deadlineType = DeadlineType.None;

			float crewMul = crewed ? 1.5f : 1.0f;
			float critMul = critical ? 1.25f : 1.0f;
			float mul = crewMul * critMul;
			CelestialBody body = chosen.body;

			switch (prestige)
			{
				case ContractPrestige.Trivial:
					SetScience(5.0f * mul, body);
					SetReputation(10.0f * mul, 5.0f * mul, body);
					SetFunds(8000.0f * mul, 25000.0f * mul, 8000.0f * mul, body);
					break;
				case ContractPrestige.Significant:
					SetScience(10.0f * mul, body);
					SetReputation(20.0f * mul, 10.0f * mul, body);
					SetFunds(20000.0f * mul, 70000.0f * mul, 20000.0f * mul, body);
					break;
				default:
					SetScience(20.0f * mul, body);
					SetReputation(40.0f * mul, 20.0f * mul, body);
					SetFunds(50000.0f * mul, 180000.0f * mul, 50000.0f * mul, body);
					break;
			}

			AddParameter(new RepairBreakdownCondition());
			return true;
		}

		void Assign(BrokenComponent chosen)
		{
			vesselPersistentId = chosen.vesselPersistentId;
			partFlightId = chosen.partFlightId;
			moduleName = chosen.moduleName ?? string.Empty;
			type = chosen.type ?? string.Empty;
			vesselName = chosen.vessel != null ? chosen.vessel.vesselName : string.Empty;
			string localized = chosen.title ?? string.Empty;
			if (!string.IsNullOrEmpty(chosen.partTitle) && !string.IsNullOrEmpty(localized) && chosen.partTitle != localized)
				componentTitle = chosen.partTitle + " (" + localized + ")";
			else
				componentTitle = !string.IsNullOrEmpty(localized) ? localized : (chosen.partTitle ?? string.Empty);
			partTitle = chosen.partTitle ?? string.Empty;
			bodyName = chosen.body != null ? Lib.BodyDisplayName(chosen.body) : string.Empty;
			crewed = chosen.crewed;
			critical = chosen.critical;
		}

		bool TooManyExisting()
		{
			if (ContractSystem.Instance == null)
				return true;

			int offered = 0;
			int active = 0;
			foreach (RepairBreakdown contract in ContractSystem.Instance.GetCurrentContracts<RepairBreakdown>())
			{
				if (object.ReferenceEquals(contract, this))
					continue;
				if (contract.ContractState == State.Offered)
					offered++;
				else if (contract.ContractState == State.Active)
					active++;
			}

			return offered >= 1 || active >= 2;
		}

		static HashSet<string> TakenKeys()
		{
			HashSet<string> taken = new HashSet<string>();
			if (ContractSystem.Instance == null)
				return taken;

			foreach (RepairBreakdown contract in ContractSystem.Instance.GetCurrentContracts<RepairBreakdown>())
			{
				if (contract.ContractState != State.Offered && contract.ContractState != State.Active)
					continue;
				taken.Add(TargetKey(contract.partFlightId, contract.moduleName, contract.type));
			}
			return taken;
		}

		static List<BrokenComponent> EligibleCandidates()
		{
			List<BrokenComponent> candidates = new List<BrokenComponent>();
			HashSet<string> taken = TakenKeys();

			foreach (BrokenComponent bc in BrokenComponent.CollectAll())
			{
				if (bc.vessel == null || bc.partFlightId == 0)
					continue;
				if (bc.vessel.situation == Vessel.Situations.PRELAUNCH)
					continue;
				if (taken.Contains(TargetKey(bc.partFlightId, bc.moduleName, bc.type)))
					continue;
				candidates.Add(bc);
			}

			return candidates;
		}

		protected override string GetHashString()
		{
			return "RepairBreakdown:" + TargetKey(partFlightId, moduleName, type);
		}

		protected override string GetTitle()
		{
			return Local.Contracts_repairTitle.Format(componentTitle, LiveVesselName());
		}

		protected override string GetDescription()
		{
			string crewNote = crewed ? Local.Contracts_repairCrewed : Local.Contracts_repairUncrewed;
			return Local.Contracts_repairDesc.Format(componentTitle, LiveVesselName(), bodyName, crewNote);
		}

		protected override string GetSynopsys()
		{
			return Local.Contracts_repairSynopsys.Format(componentTitle, LiveVesselName());
		}

		protected override string GetNotes()
		{
			return crewed ? Local.Contracts_repairCrewed : Local.Contracts_repairUncrewed;
		}

		protected override string MessageCompleted()
		{
			return Local.Contracts_repairComplete.Format(componentTitle, LiveVesselName());
		}

		protected override string MessageFailed()
		{
			return Local.Contracts_repairFailed.Format(componentTitle, vesselName);
		}

		public override bool MeetRequirements()
		{
			if (partFlightId != 0)
				return true;
			return EligibleCandidates().Count > 0;
		}

		protected override void OnSave(ConfigNode node)
		{
			node.AddValue("vesselPersistentId", vesselPersistentId);
			node.AddValue("partFlightId", partFlightId);
			node.AddValue("moduleName", moduleName);
			node.AddValue("type", type);
			node.AddValue("vesselName", vesselName);
			node.AddValue("componentTitle", componentTitle);
			node.AddValue("partTitle", partTitle);
			node.AddValue("bodyName", bodyName);
			node.AddValue("crewed", crewed);
			node.AddValue("critical", critical);
		}

		protected override void OnLoad(ConfigNode node)
		{
			vesselPersistentId = Lib.ConfigValue(node, "vesselPersistentId", 0u);
			partFlightId = Lib.ConfigValue(node, "partFlightId", 0u);
			moduleName = Lib.ConfigValue(node, "moduleName", string.Empty);
			type = Lib.ConfigValue(node, "type", string.Empty);
			vesselName = Lib.ConfigValue(node, "vesselName", string.Empty);
			componentTitle = Lib.ConfigValue(node, "componentTitle", string.Empty);
			partTitle = Lib.ConfigValue(node, "partTitle", string.Empty);
			bodyName = Lib.ConfigValue(node, "bodyName", string.Empty);
			crewed = Lib.ConfigValue(node, "crewed", false);
			critical = Lib.ConfigValue(node, "critical", false);
		}

		protected override void OnUpdate()
		{
			if (ContractState != State.Offered && ContractState != State.Active)
				return;

			if (!BrokenComponent.TryGetState(partFlightId, moduleName, type, out Vessel v, out bool broken))
			{
				if (ContractState == State.Offered)
				{
					if (missingRealtime <= 0f)
						missingRealtime = Time.realtimeSinceStartup;
					else if (Time.realtimeSinceStartup - missingRealtime > 8f)
						Withdraw();
				}
				return;
			}

			missingRealtime = 0f;
			vesselPersistentId = v.persistentId;
			vesselName = v.vesselName;

			if (!broken && ContractState == State.Offered)
				Withdraw();
		}
	}


	public sealed class RepairBreakdownCondition : ContractParameter
	{
		RepairBreakdown Host => Root as RepairBreakdown;

		protected override string GetHashString()
		{
			RepairBreakdown host = Host;
			if (host == null)
				return "RepairBreakdownCondition";
			return "RepairBreakdownCondition:" + RepairBreakdown.TargetKey(host.partFlightId, host.moduleName, host.type);
		}

		protected override string GetTitle()
		{
			RepairBreakdown host = Host;
			if (host == null)
				return string.Empty;
			return Local.Contracts_repairParam.Format(host.componentTitle, host.LiveVesselName());
		}

		protected override void OnRegister()
		{
			API.OnReliabilityStateChanged.Add(OnReliabilityChanged);
		}

		protected override void OnUnregister()
		{
			API.OnReliabilityStateChanged.Remove(OnReliabilityChanged);
		}

		void OnReliabilityChanged(Vessel vessel, uint partFlightId, string moduleName, string type, bool broken, bool critical)
		{
			if (broken)
				return;
			if (Root == null || Root.ContractState != Contract.State.Active)
				return;

			RepairBreakdown host = Host;
			if (host == null)
				return;
			if (partFlightId != host.partFlightId)
				return;
			if (moduleName != host.moduleName)
				return;
			if (!string.Equals(type, host.type, StringComparison.Ordinal))
				return;

			SetComplete();
		}

		protected override void OnUpdate()
		{
			if (Root == null || Root.ContractState != Contract.State.Active)
				return;

			RepairBreakdown host = Host;
			if (host == null)
				return;
			if (!BrokenComponent.TryGetState(host.partFlightId, host.moduleName, host.type, out _, out bool broken))
				return;
			if (!broken)
				SetComplete();
		}
	}
}

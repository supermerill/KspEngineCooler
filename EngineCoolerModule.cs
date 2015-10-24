/*
Copyright [2015] Merill

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 * */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace EngineCooler
{

	public class EngineCoolerModule : PartModule, IPartCostModifier
	{

		[KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "mode")]
		public string mode = "";//"radiative";

		[KSPField]
		public float nbAblator = 100;

		[KSPField]
		public string techAblator = null;

		[KSPField]
		public float costRegenerativePercent = 50;

		[KSPField]
		public float regenerativeHeat = 192f;

		[KSPField]
		public string techRegenerative = null;

		//only on part prefab ?
		public ConfigNode ablatorNode = null;

		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);
			//Debug.Log("[EC] : techAblator=" + techAblator + " techRegenerative=" + techRegenerative);
			//Debug.Log("[EC] : techAblator ? " + ResearchAndDevelopment.GetTechnologyState(techAblator)
			//	+ " techRegenerative ? " + ResearchAndDevelopment.GetTechnologyState(techRegenerative));
			if (state == StartState.Editor && part.partInfo.partPrefab != this && mode == "")
			{
				if (techRegenerative == null || ResearchAndDevelopment.GetTechnologyState(techRegenerative) == RDTech.State.Available)
				{
					switchToRegenerative();
				}
				else if (techAblator == null || ResearchAndDevelopment.GetTechnologyState(techAblator) == RDTech.State.Available)
				{
					switchToAblator();
				}
				else
				{
					switchToRadiative();
				}
			}
		}

		public void switchToAblator()
		{
			mode = "ablator";
			addResource("Ablator", nbAblator);
			//do the same for all symparts
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineCoolerModule)symPart.Modules["EngineCoolerModule"]).mode = "Ablator";
				((EngineCoolerModule)symPart.Modules["EngineCoolerModule"]).addResource("Ablator", nbAblator);
			}
		}
		public void switchFromAblator()
		{
			removeResource("Ablator");
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineCoolerModule)symPart.Modules["EngineCoolerModule"]).removeResource("Ablator");
			}
		}

		public void switchToRegenerative()
		{

			mode = "regenerative";
			//do the same for all symparts
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineCoolerModule)symPart.Modules["EngineCoolerModule"]).mode = "regenerative";
			}
		}

		public void switchToRadiative()
		{
			mode = "radiative";
			//do the same for all symparts
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineCoolerModule)symPart.Modules["EngineCoolerModule"]).mode = "radiative";
			}
		}

		[KSPEvent(guiActiveEditor = true, guiActive = false, guiName = "Switch Cooler")]
		public void switchMode()
		{
			try
			{
				if (mode == "")
				{
					switchToRadiative();
				}

				if (mode == "ablator")
				{
					switchFromAblator();
				}

				switch (mode)
				{
					case "radiative":
						mode = "ablator";
						if (techAblator == null || ResearchAndDevelopment.GetTechnologyState(techAblator) == RDTech.State.Available)
						{
							switchToAblator();
						}
						else
						{
							switchMode();
						}
						break;
					case "ablator":
						mode = "regenerative";
						if (techRegenerative == null || ResearchAndDevelopment.GetTechnologyState(techRegenerative) == RDTech.State.Available)
						{
							switchToRegenerative();
						}
						else
						{
							switchMode();
						}
						break;
					case "regenerative":
						mode = "radiative";
						switchToRadiative();
						break;
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[EngineCoolerModule]ERROR " + e);
			}
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		public float GetModuleCost(float defaultCost)
		{
			if (mode == "regenerative")
			{
				return defaultCost * costRegenerativePercent / 100;
			}
			if (mode == "ablator")
			{
				PartResource ablatorRessource = part.Resources["Ablator"];
				return nbAblator * ablatorRessource.info.unitCost;
			}
			return 0;
		}

		public void removeResource(string name)
		{
			List<PartResource> allResource = new List<PartResource>();
			allResource.AddRange(part.Resources.list);
			foreach (PartResource res in allResource)
			{
				if (res.resourceName == name)
				{
					part.Resources.list.Remove(res);
					Destroy(res);
				}
			}
		}

		// return maxfuelmass
		public void addResource(string name, float amount)
		{
			ConfigNode newPartResource = new ConfigNode("RESOURCE");
			newPartResource.AddValue("name", name);
			newPartResource.AddValue("amount", amount / 2);
			newPartResource.AddValue("maxAmount", amount);
			PartResource res = part.AddResource(newPartResource);
		}

		public PartModule createModule(ConfigNode nodeToCreate)
		{
			string moduleName = nodeToCreate.GetValue("name");
			if (moduleName == null) return null;

			PartModule newMod = part.AddModule(moduleName);
			MethodInfo awakeMethod = typeof(PartModule).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
			awakeMethod.Invoke(newMod, new object[] { });
			// uses reflection to find and call the PartModule.Awake() private method
			newMod.Load(nodeToCreate);
			return newMod;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (ablatorNode == null && node.HasNode("MODULE"))
			{
				ablatorNode = node.GetNode("MODULE");
			}


			//load ablator module if not present
			if (/*vessel == null && */HighLogic.LoadedSceneIsFlight && mode == "ablator")
			{
				try
				{
					ConfigNode ablatrorModuleNode = (part.partInfo.partPrefab.Modules["EngineCoolerModule"] as EngineCoolerModule).ablatorNode;

					//quasi-always true (as it's not in the part prefab)
					if (!part.Modules.Contains(ablatrorModuleNode.GetValue("name")))
					{
						PartModule moduleAblator = createModule(ablatrorModuleNode);
					}
				}
				catch (Exception e)
				{
					Debug.Log("[EngineCooler] Error: " + e);
				}
			}

			//it's maybe erased by the part prefab at loading, re-set it.
			if (mode == "regenerative")
			{
				try
				{
					PartModule moduleEngineRestore = part.Modules["ModuleEngines"];
					if (moduleEngineRestore == null) moduleEngineRestore = part.Modules["ModuleEnginesFX"];
					(moduleEngineRestore as ModuleEngines).heatProduction = this.regenerativeHeat;
				}
				catch (Exception e)
				{
					Debug.Log("[EngineCooler] Error: " + e);
				}
			}

		}

		//unused
		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			if (ablatorNode != null)
			{
				node.AddNode(ablatorNode);
			}
		}

		public override void OnFixedUpdate()
		{
			//Debug.Log("[EngineCooler] : OnFixedUpdate");
			//List<ModuleEngines> list = part.FindModulesImplementing<ModuleEngines>();
			//if (list.Count > 0 && list[0].EngineIgnited)
			//{
			//	Debug.Log("[EngineCooler] skinInternalConductionMult : " + part.skinInternalConductionMult);
			//	Debug.Log("[EngineCooler] skinSkinConductionMult : " + part.skinSkinConductionMult);
			//	Debug.Log("[EngineCooler] ShieldedFromAirstream : " + part.ShieldedFromAirstream);
			//	Debug.Log("[EngineCooler] skinExposedArea : " + part.skinExposedArea);
			//	Debug.Log("[EngineCooler] skinExposedAreaFrac : " + part.skinExposedAreaFrac);
			//	Debug.Log("[EngineCooler] skinToInternalFlux : " + part.skinToInternalFlux);
			//	Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
			//	Debug.Log("[EngineCooler] skinUnexposedMassMult : " + part.skinUnexposedMassMult);
			//	Debug.Log("[EngineCooler] exposedArea : " + part.exposedArea);
			//	Debug.Log("[EngineCooler] aerodynamicArea : " + part.aerodynamicArea);
			//	Debug.Log("[EngineCooler] thermalInternalFlux : " + part.thermalInternalFlux);
			//	Debug.Log("[EngineCooler] thermalInternalFluxPrevious : " + part.thermalInternalFluxPrevious);
			//	Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
			//	Debug.Log("[EngineCooler] skinMaxTemp : " + part.skinMaxTemp);
			//	Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
			//	Debug.Log("[EngineCooler] temperature : " + part.temperature);
			//	Debug.Log("[EngineCooler] maxTemp : " + part.maxTemp);
			//	Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
			//	//part.ShieldedFromAirstream = false;
			//	//part.skinExposedArea = part.aerodynamicArea;
			//	//part.skinExposedAreaFrac = 1;
			//	part.ShieldedFromAirstream = true;
			//}
			//else part.ShieldedFromAirstream = false;
			base.OnFixedUpdate();
			//check if engine is ignited, if it's in ablator mode, 
			//	if the skin should be heated (to break reentry a little less)
			List<ModuleEngines> List = part.FindModulesImplementing<ModuleEngines>();
			if (List.Count > 0 && List[0].EngineIgnited && List[0].fuelFlowGui > 0 && mode == "ablator"
				&& part.skinTemperature <= part.temperature)
			{
				//Debug.Log("[EngineCooler] skinInternalConductionMult : " + part.skinInternalConductionMult);
				//Debug.Log("[EngineCooler] skinSkinConductionMult : " + part.skinSkinConductionMult);
				//Debug.Log("[EngineCooler] ShieldedFromAirstream : " + part.ShieldedFromAirstream);
				//Debug.Log("[EngineCooler] skinExposedArea : " + part.skinExposedArea);
				//Debug.Log("[EngineCooler] skinExposedAreaFrac : " + part.skinExposedAreaFrac);
				//Debug.Log("[EngineCooler] skinToInternalFlux : " + part.skinToInternalFlux);
				//Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
				//Debug.Log("[EngineCooler] skinUnexposedMassMult : " + part.skinUnexposedMassMult);
				//Debug.Log("[EngineCooler] exposedArea : " + part.exposedArea);
				//Debug.Log("[EngineCooler] aerodynamicArea : " + part.aerodynamicArea);
				//Debug.Log("[EngineCooler] thermalInternalFlux : " + part.thermalInternalFlux);
				//Debug.Log("[EngineCooler] thermalInternalFluxPrevious : " + part.thermalInternalFluxPrevious);
				//Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
				//Debug.Log("[EngineCooler] skinMaxTemp : " + part.skinMaxTemp);
				//Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
				//Debug.Log("[EngineCooler] temperature : " + part.temperature);
				//Debug.Log("[EngineCooler] maxTemp : " + part.maxTemp);
				//Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
				//part.ShieldedFromAirstream = false;
				//part.skinExposedArea = part.aerodynamicArea;
				//part.skinExposedAreaFrac = 1;

				// aero mess things with skinExposedArea who can freeze the conduction... so deactivate it with shield
				part.ShieldedFromAirstream = true;
				part.skinInternalConductionMult = 10000000;
			}
			else
			{
				//return to normal
				part.ShieldedFromAirstream = false;
				part.skinInternalConductionMult = 1;
			}
		}

		//public override void OnUpdate()
		//{
		//	Debug.Log("[EngineCooler] : update");
		//	List<ModuleEngines> list = part.FindModulesImplementing<ModuleEngines>();
		//	if (list.Count > 0 && list[0].EngineIgnited)
		//	{
		//		Debug.Log("[EngineCooler] skinInternalConductionMult : " + part.skinInternalConductionMult);
		//		Debug.Log("[EngineCooler] skinSkinConductionMult : " + part.skinSkinConductionMult);
		//		Debug.Log("[EngineCooler] ShieldedFromAirstream : " + part.ShieldedFromAirstream);
		//		Debug.Log("[EngineCooler] skinExposedArea : " + part.skinExposedArea);
		//		Debug.Log("[EngineCooler] skinExposedAreaFrac : " + part.skinExposedAreaFrac);
		//		Debug.Log("[EngineCooler] skinToInternalFlux : " + part.skinToInternalFlux);
		//		Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
		//		Debug.Log("[EngineCooler] skinUnexposedMassMult : " + part.skinUnexposedMassMult);
		//		Debug.Log("[EngineCooler] exposedArea : " + part.exposedArea);
		//		Debug.Log("[EngineCooler] aerodynamicArea : " + part.aerodynamicArea);
		//		Debug.Log("[EngineCooler] thermalInternalFlux : " + part.thermalInternalFlux);
		//		Debug.Log("[EngineCooler] thermalInternalFluxPrevious : " + part.thermalInternalFluxPrevious);
		//		Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
		//		Debug.Log("[EngineCooler] skinMaxTemp : " + part.skinMaxTemp);
		//		Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
		//		Debug.Log("[EngineCooler] temperature : " + part.temperature);
		//		Debug.Log("[EngineCooler] maxTemp : " + part.maxTemp);
		//		Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
		//		//part.ShieldedFromAirstream = false;
		//		//part.skinExposedArea = part.aerodynamicArea;
		//		//part.skinExposedAreaFrac = 0.9;
		//	}
		//	base.OnUpdate();
		//	List<ModuleEngines> List = part.FindModulesImplementing<ModuleEngines>();
		//	if(List.Count>0 && List[0].EngineIgnited)
		//	{

		//		Debug.Log("[EngineCooler] skinInternalConductionMult : " + part.skinInternalConductionMult);
		//		Debug.Log("[EngineCooler] skinSkinConductionMult : " + part.skinSkinConductionMult);
		//		Debug.Log("[EngineCooler] ShieldedFromAirstream : " + part.ShieldedFromAirstream);
		//		Debug.Log("[EngineCooler] skinExposedArea : " + part.skinExposedArea);
		//		Debug.Log("[EngineCooler] skinExposedAreaFrac : " + part.skinExposedAreaFrac);
		//		Debug.Log("[EngineCooler] skinToInternalFlux : " + part.skinToInternalFlux);
		//		Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
		//		Debug.Log("[EngineCooler] skinUnexposedMassMult : " + part.skinUnexposedMassMult);
		//		Debug.Log("[EngineCooler] exposedArea : " + part.exposedArea);
		//		Debug.Log("[EngineCooler] aerodynamicArea : " + part.aerodynamicArea);
		//		Debug.Log("[EngineCooler] thermalInternalFlux : " + part.thermalInternalFlux);
		//		Debug.Log("[EngineCooler] thermalInternalFluxPrevious : " + part.thermalInternalFluxPrevious);
		//		Debug.Log("[EngineCooler] skinTemperature : " + part.skinTemperature);
		//		Debug.Log("[EngineCooler] skinMaxTemp : " + part.skinMaxTemp);
		//		Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
		//		Debug.Log("[EngineCooler] temperature : " + part.temperature);
		//		Debug.Log("[EngineCooler] maxTemp : " + part.maxTemp);
		//		Debug.Log("[EngineCooler] skinUnexposedExternalTemp : " + part.skinUnexposedExternalTemp);
		//		//part.ShieldedFromAirstream = false;
		//		//part.skinExposedArea = part.aerodynamicArea;
		//		//part.skinExposedAreaFrac = 0.9;
		//	}
		//}
	}
}

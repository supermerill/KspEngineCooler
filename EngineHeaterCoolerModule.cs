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
	/**
	 * Re-writing all heating and cooling stuff to take ownership and undestand how they work.
	 * Now, all these formulea are automatically scaled by thrust. We assume the surface aera is scaled the same way for the models
	 * Heating: add {degreePerSecond} degree each second (just after cooling) at full fuel flow
	 * Radiative cooling : black body radiation (unobscured): z*T^4, with z to be at equilibrium with 80% thrust at maxTemp.
	 * Ablative cooling: nb of abator scaled by thrust, {ablatorLossConst}% ablator => remove {ablatorPyrolysisLossFactor} degrees
	 * Regenerative cooling => asymptote to maxtemp*0.75
	 */
	public class EngineHeaterCoolerModule : PartModule, IPartCostModifier
	{

		[KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "mode")]
		public string mode = "";//"radiative";

		///////////// heater

		[KSPField]
		public float degreePerSecond = 20f;

		///////////// radiative

		[KSPField]
		public float radiativeEfficiency = 0.8f;

		///////////// ablator

		[KSPField]
		public float nbAblatorPerThrust = 1;

		[KSPField]
		public float ablatorPyrolysisLossFactor = 190;

		[KSPField]
		public float ablatorLossConst = 0.01f;

		[KSPField]
		public float ablatorMinTemp = 1000f;

		[KSPField]
		public string techAblator = null;

		//only on part prefab ?
		//public ConfigNode ablatorNode = null;

		///////////// regenerative

		[KSPField]
		public float costRegenerativePercent = 50;

		[KSPField]
		public float regenerativeHeat = 300f;

		[KSPField]
		public string techRegenerative = null;


		public override void OnStart(PartModule.StartState state)
		{
			base.OnStart(state);
			//quasi-remove default radiative cooling, as it's buggy when attaching side part to the tank of the engine.
			//part.emissiveConstant = 0.01f;

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

		public virtual void switchToAblator()
		{
			mode = "ablator";
			addResource("Ablator", (float)(nbAblatorPerThrust * getEngine().GetMaxThrust() /*part.mass*/));
			//do the same for all symparts
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineHeaterCoolerModule)symPart.Modules["EngineHeaterCoolerModule"]).mode = "ablator";
				((EngineHeaterCoolerModule)symPart.Modules["EngineHeaterCoolerModule"]).addResource("Ablator",
					(float)(nbAblatorPerThrust * getEngine().GetMaxThrust() /* part.mass*/));
			}
		}
		public virtual void switchFromAblator()
		{
			removeResource("Ablator");
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineHeaterCoolerModule)symPart.Modules["EngineHeaterCoolerModule"]).removeResource("Ablator");
			}
		}

		public virtual void switchToRegenerative()
		{

			mode = "regenerative";
			//do the same for all symparts
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineHeaterCoolerModule)symPart.Modules["EngineHeaterCoolerModule"]).mode = "regenerative";
			}
		}

		public virtual void switchToRadiative()
		{
			mode = "radiative";
			//do the same for all symparts
			foreach (Part symPart in part.symmetryCounterparts)
			{
				((EngineHeaterCoolerModule)symPart.Modules["EngineHeaterCoolerModule"]).mode = "radiative";
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
				Debug.LogError("[EngineHeaterCoolerModule]ERROR " + e);
			}
			GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		public virtual float GetModuleCost(float defaultCost)
		{
			if (mode == "regenerative")
			{
				return defaultCost * costRegenerativePercent / 100;
			}
			if (mode == "ablator")
			{
				PartResource ablatorRessource = part.Resources["Ablator"];
				return (float)(nbAblatorPerThrust * getEngine().GetMaxThrust() /*part.mass*/) * ablatorRessource.info.unitCost;
			}
			return 0;
		}

		protected void removeResource(string name)
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
		protected void addResource(string name, float amount)
		{
			ConfigNode newPartResource = new ConfigNode("RESOURCE");
			newPartResource.AddValue("name", name);
			newPartResource.AddValue("amount", amount / 2);
			newPartResource.AddValue("maxAmount", amount);
			PartResource res = part.AddResource(newPartResource);
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
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
					Debug.LogError("[EngineCooler] Error: " + e);
				}
			}
		}

		private ModuleEngines moduleEngine = null;
		protected ModuleEngines getEngine()
		{
			if (moduleEngine == null)
			{
				if (moduleEngine == null && part.Modules.Contains("ModuleEnginesFX"))
					moduleEngine = part.Modules["ModuleEnginesFX"] as ModuleEnginesFX;
				
				if (moduleEngine == null && part.Modules.Contains("ModuleEngines"))
					moduleEngine = part.Modules["ModuleEngines"] as ModuleEngines;
			}
			return moduleEngine;
		}

		private PartResource ablatorRessource = null;
		protected PartResource getAblator()
		{
			if (ablatorRessource == null)
			{
				Debug.Log(part.name + " ablator? " + part.Resources.Contains("Ablator"));
				ablatorRessource = part.Resources["Ablator"];
			}
			return ablatorRessource;
		}

		private double lastTime = 0;
		//it's not called when activated via button
		public override void OnFixedUpdate()
		{
			base.OnFixedUpdate();
			double currentTime = Planetarium.GetUniversalTime();
			getEngine();

			if (lastTime != 0
				&& moduleEngine.EngineIgnited
				&& moduleEngine.currentThrottle > 0 && moduleEngine.fuelFlowGui > 0)
			{
				double delta = currentTime - lastTime;
				if (mode == "regenerative")
				{
					useRegenerative(delta);
				}
				else if (mode == "ablator")
				{
					//ablat yourself
					if (part.skinTemperature > ablatorMinTemp)
					{
						useAblator(delta);
					}
				}
				else
				{
					useRadiative(delta);
				}

				addHeat(delta);

			}
			lastTime = currentTime;
		}

		public virtual void useRadiative(double deltaTime)
		{
			//stock is inconsitant with radial-attach things


			//Stefan-Boltzmann : M(t) = o * T^4
			double linear = (part.skinTemperature / part.maxTemp);
			double linearExternal = (vessel.externalTemperature / part.maxTemp);
			double quadra = Math.Pow(linear, 4);
			double quadraExternal = (vessel.externalTemperature / part.maxTemp);

			//choose etween linear or quadra
			part.skinTemperature -= deltaTime * degreePerSecond * (quadra - quadraExternal) * radiativeEfficiency;

			//default parameters,
			//space:
			//45" for 100% => 45fu burned
			//58" for 90% => 52fu burned
			//1'10" for 85% => 59fu burned
			// 1995° for 80% => oo fu burned

			//ASL (with 300K ambiant):
			// 40" for 100% => 40fu
			// 45" for 90% => 40.5fu
			// 50" for 85% => 42.5fu
			// 55" for 80% => 44fu
			//
			// 1'20 for 70% => 56fu
		}

		public virtual void useRegenerative(double deltaTime)
		{
			//nothing to radiate: the fuel gather all heat

			// asymptote to 1500
			part.skinTemperature += deltaTime * degreePerSecond
				* (moduleEngine.fuelFlowGui / moduleEngine.maxFuelFlow)
				* (((part.maxTemp * 0.75) - part.skinTemperature) / (part.maxTemp * 0.75));
		}

		public virtual void useAblator(double deltaTime)
		{
			//radiate a little: the ablator isolate, but engine can be run oo at ~32%
			useRadiative(deltaTime * ( getAblator().maxAmount/ (getAblator().maxAmount - getAblator().amount)) * 0.4 );

			double coeffAblator = (part.skinTemperature - ablatorMinTemp) / ((part.maxTemp - 10) - ablatorMinTemp);
			//coef : 0->10
			coeffAblator = 1 / (1 - (coeffAblator * coeffAblator)) - 1;
			//coeff is now infinite when skintemp==MaxT and ==0 when skintemp==0
			// == 0.33 when skintemp==0.75MaxT, ==1 when skintemp==0.85MaxT

			//remove ablatorLossConst% of max to cancel the heat
			double nbAblatorRemoved = getAblator().amount * ablatorLossConst * deltaTime * coeffAblator;
			getAblator().amount -= nbAblatorRemoved;
			//remove heat
			part.skinTemperature -= deltaTime * degreePerSecond * ablatorPyrolysisLossFactor * (nbAblatorRemoved / (getAblator().maxAmount * deltaTime));
			
		}

		public virtual void addHeat(double deltaTime)
		{
			part.skinTemperature += deltaTime * degreePerSecond
						* (moduleEngine.fuelFlowGui / moduleEngine.maxFuelFlow);
			//or use requestedMassFlow? resultingThrust?
		}

		public override void OnUpdate()
		{
			//debug from ksp
			if (Planetarium.GetUniversalTime() - lastTime > 1)
			{
				OnFixedUpdate();
			}
		}

	}
}

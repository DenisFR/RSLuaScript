/*=============================================================================|
|  PROJECT RSLuaScript                                                   1.0.0 |
|==============================================================================|
|  Copyright (C) 2018 Denis FRAIPONT (SICA2M)                                  |
|  All rights reserved.                                                        |
|==============================================================================|
|  RSLuaScript is free software: you can redistribute it and/or modify         |
|  it under the terms of the Lesser GNU General Public License as published by |
|  the Free Software Foundation, either version 3 of the License, or           |
|  (at your option) any later version.                                         |
|                                                                              |
|  It means that you can distribute your commercial software which includes    |
|  RSLuaScript without the requirement to distribute the source code           |
|  of your application and without the requirement that your application be    |
|  itself distributed under LGPL.                                              |
|                                                                              |
|  RSLuaScript    is distributed in the hope that it will be useful,           |
|  but WITHOUT ANY WARRANTY; without even the implied warranty of              |
|  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the               |
|  Lesser GNU General Public License for more details.                         |
|                                                                              |
|  You should have received a copy of the GNU General Public License and a     |
|  copy of Lesser GNU General Public License along with RSLuaScript.           |
|  If not, see  http://www.gnu.org/licenses/                                   |
|=============================================================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Stations;

//Install this NuGet Package version:1.0.6752.15716
//https://www.nuget.org/packages/KopiLua/1.0.6752.15716
using static KopiLua.Lua;
//You can get Lua manual here:
//https://www.lua.org/manual/5.1/manual.html


namespace RSLuaScript
{
	/// <summary>
	/// Code-behind class for the RSLuaScript Smart Component.
	/// </summary>
	/// <remarks>
	/// The code-behind class should be seen as a service provider used by the 
	/// Smart Component runtime. Only one instance of the code-behind class
	/// is created, regardless of how many instances there are of the associated
	/// Smart Component.
	/// Therefore, the code-behind class should not store any state information.
	/// Instead, use the SmartComponent.StateCache collection.
	/// </remarks>
	public class CodeBehind : SmartComponentCodeBehind
	{
		//Principal Component is OnLoad (don't do anything)
		bool bOnLoad = false;
		//Private Component List for EventHandler
		private static readonly List<SmartComponent> myComponents = new List<SmartComponent>();
		//Last time an update occurs (when received a lot of event log in same time).
		DateTime lastUpdate = DateTime.UtcNow;
		//If component is on OnPropertyValueChanged
		Dictionary<string, bool> isOnPropertyValueChanged;
		//Lua interpreter per component
		Dictionary<string, lua_State> luaStates;
		//Last Simulation time per component
		private static readonly Dictionary<string, double> lastSimulTimes = new Dictionary<string, double>();

		/// <summary>
		/// Called from [!:SmartComponent.InitializeCodeBehind]. 
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnInitialize(SmartComponent component)
		{
			///Never Called???
			base.OnInitialize(component);
			component.Properties["Status"].Value = "OnInitialize";
		}

		/// <summary>
		/// Called if the library containing the SmartComponent has been replaced
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnLibraryReplaced(SmartComponent component)
		{
			base.OnLibraryReplaced(component);
			component.Properties["Status"].Value = "OnLibraryReplaced";

			UpdateScriptIOSignals(component);
			//Save Modified Component for EventHandlers
			//OnPropertyValueChanged is not called here
			UpdateComponentList(component);
		}

		/// <summary>
		/// Called when the library or station containing the SmartComponent has been loaded 
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnLoad(SmartComponent component)
		{
			base.OnLoad(component);
			bOnLoad = true;

			string dllPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
											+ "\\RobotStudio\\Libraries\\KopiLua.dll";
			if (!System.IO.File.Exists(dllPath))
			{
				if (component.Assets.TryGetAsset("KopiLua.dll", out Asset asset))
					System.IO.File.WriteAllBytes(dllPath, asset.GetData());

				Logger.AddMessage("RSLuaScript: KopiLua Dll copied to your Libraries folder.", LogMessageSeverity.Warning);
			}

			AppDomain CurrentDomain = AppDomain.CurrentDomain;
			CurrentDomain.AssemblyResolve += new ResolveEventHandler(MyResolver);

			isOnPropertyValueChanged = new Dictionary<string, bool>();
			//luaStates = new Dictionary<string, lua_State>();//Can't handle it here else an error occurs with assembly missing.

			component.Properties["Status"].Value = "OnLoad";
			//Here component is not the final component and don't get saved properties.
			//Only called once for all same instance.

			// Survey log message
			Logger.LogMessageAdded -= OnLogMessageAdded;
			Logger.LogMessageAdded += OnLogMessageAdded;

			bOnLoad = false;
		}

		/// <summary>
		/// ResolveEventHandler to handle KopiLua missing file, after OnLoad copied it.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="args">The event data.</param>
		/// <returns>The assembly that resolves the type, assembly, or resource; or null if the assembly cannot be resolved.</returns>
		private static Assembly MyResolver(object sender, ResolveEventArgs args)
		{
			//Logger.AddMessage("AssembyResolve " + args.Name + " " + args.RequestingAssembly, LogMessageSeverity.Warning);
			string dllPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
				                 + "\\RobotStudio\\Libraries\\KopiLua.dll";

			int index = args.Name.IndexOf(',');
			if (index == -1)
			{
				return null;
			}
			string name = args.Name.Substring(0, index);

			if (name.ToLower() == "kopilua")
			{
				string fullPath = System.IO.Path.GetFullPath(dllPath);
				if (System.IO.File.Exists(fullPath))
				{
					return Assembly.LoadFrom(fullPath);
				}
			}

			return null;
		}

		/// <summary>
		/// Called when the value of a dynamic property value has changed.
		/// </summary>
		/// <param name="component"> Component that owns the changed property. </param>
		/// <param name="changedProperty"> Changed property. </param>
		/// <param name="oldValue"> Previous value of the changed property. </param>
		public override void OnPropertyValueChanged(SmartComponent component, DynamicProperty changedProperty, Object oldValue)
		{
			if (changedProperty.Name == "Status")
				return;

			base.OnPropertyValueChanged(component, changedProperty, oldValue);
			if (bOnLoad)
			{
				UpdateComponentList(component);
				isOnPropertyValueChanged[component.UniqueId] = false;
				return;
			}
			if (!isOnPropertyValueChanged.ContainsKey(component.UniqueId))
				isOnPropertyValueChanged[component.UniqueId] = false;

			bool bIsOnPropertyValueChanged = isOnPropertyValueChanged[component.UniqueId];
			isOnPropertyValueChanged[component.UniqueId] = true;

			if (changedProperty.Name == "LuaFile")
			{
			 LoadFile(component);
			}

			if (!bIsOnPropertyValueChanged)
			{
				//Save Modified Component for EventHandlers
				UpdateComponentList(component);
			}

			isOnPropertyValueChanged[component.UniqueId] = bIsOnPropertyValueChanged;
		}

		/// <summary>
		/// Called when the value of an I/O signal value has changed.
		/// </summary>
		/// <param name="component"> Component that owns the changed signal. </param>
		/// <param name="changedSignal"> Changed signal. </param>
		public override void OnIOSignalValueChanged(SmartComponent component, IOSignal changedSignal)
		{
			if (changedSignal.Name == "LoadFile")
			{
				if ((int)changedSignal.Value == 1)
				{
					string initialDir = component.ContainingProject?.FileInfo?.DirectoryName ?? "";

					// First look at Example file
					if (!string.IsNullOrEmpty(initialDir))
					{
						if (!System.IO.File.Exists(initialDir + "\\RSLuaScript.lua"))
						{
							if (component.Assets.TryGetAsset("RSLuaScript.lua", out Asset asset))
								System.IO.File.WriteAllBytes(initialDir + "\\RSLuaScript.lua", asset.GetData());
						}
					}

					System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
					{
						Title = "Open Lua Script File",
						InitialDirectory = initialDir,
						Filter = "Lua files (*.lua)|*.lua|All files (*.*)|*.*",
						RestoreDirectory = true,
						FileName = (string)component.Properties["LuaFile"].Value
					};

					if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					{
						if ((string)component.Properties["LuaFile"].Value == openFileDialog.FileName)
							if (System.Windows.Forms.MessageBox.Show("Do you want to reopen this file, then execute main script once more?"
																											, "Same file reopened"
																											, System.Windows.Forms.MessageBoxButtons.YesNo
																											, System.Windows.Forms.MessageBoxIcon.Question
																											) == System.Windows.Forms.DialogResult.Yes)
								component.Properties["LuaFile"].Value = ""; //To raise OnPropertyValueChanged

						component.Properties["LuaFile"].Value = openFileDialog.FileName;
						component.Properties["Status"].Value = "Lua File Loaded";
					}

				}
			}
			else
			{
				if ( changedSignal.SignalType == IOSignalType.AnalogInput 
					|| changedSignal.SignalType == IOSignalType.DigitalGroupInput 
					|| changedSignal.SignalType == IOSignalType.DigitalInput)
					UpdateScriptIOSignals(component, changedSignal.Name, Convert.ToDouble(changedSignal.Value));
			}

			UpdateComponentList(component);
		}

		/// <summary>
		/// Called during simulation.
		/// </summary>
		/// <param name="component"> Simulated component. </param>
		/// <param name="simulationTime"> Time (in ms) for the current simulation step. </param>
		/// <param name="previousTime"> Time (in ms) for the previous simulation step. </param>
		/// <remarks>
		/// For this method to be called, the component must be marked with
		/// simulate="true" in the xml file.
		/// </remarks>
		public override void OnSimulationStep(SmartComponent component, double simulationTime, double previousTime)
		{
			//Check connections output. Sometimes input doesn't trigger output change.
			if (component.ContainingProject is Station station)
				foreach (IOConnection ioConn in station.Connections)
				{
					if (ioConn.TargetObjectName == component.Name)
						ioConn.Update();
				}

			lastSimulTimes[component.UniqueId] = simulationTime;

			UpdateScriptSim(component);
			UpdateComponentList(component);
		}

		/// <summary>
		/// Called to retrieve the actual value of a property attribute with the dummy value '?'.
		/// </summary>
		/// <param name="component">Component that owns the property.</param>
		/// <param name="owningProperty">Property that owns the attribute.</param>
		/// <param name="attributeName">Name of the attribute to query.</param>
		/// <returns>Value of the attribute.</returns>
		public override string QueryPropertyAttributeValue(SmartComponent component, DynamicProperty owningProperty, string attributeName)
		{
			return "?";
		}

		/// <summary>
		/// Called to validate the value of a dynamic property with the CustomValidation attribute.
		/// </summary>
		/// <param name="component">Component that owns the changed property.</param>
		/// <param name="property">Property that owns the value to be validated.</param>
		/// <param name="newValue">Value to validate.</param>
		/// <returns>Result of the validation. </returns>
		public override ValueValidationInfo QueryPropertyValueValid(SmartComponent component, DynamicProperty property, object newValue)
		{
			return ValueValidationInfo.Valid;
		}


		//*********************************************************************************************
		/// <summary>
		/// Update internal component list to get them in EventHandler
		/// </summary>
		/// <param name="component">Component to update.</param>
		protected void UpdateComponentList(SmartComponent component)
		{
			bool bFound = false;
			for (int i = 0; i < myComponents.Count; ++i)
			{
				SmartComponent myComponent = myComponents[i];
				//Test if component exists as no OnUnLoad event exists.
				if ( (myComponent.ContainingProject == null)
					  || (myComponent.ContainingProject.GetObjectFromUniqueId(myComponents[i].UniqueId) == null)
						|| (myComponent.ContainingProject.Name == "")
						|| (bFound && (myComponent.UniqueId == component.UniqueId)) )
				{
					Logger.AddMessage("RSLuaScript: Remove old Component " + myComponents[i].Name + " from cache. This component works only with named station.", LogMessageSeverity.Information);
					myComponents.Remove(myComponent);
					--i;
					continue;
				}
				if (myComponents[i].UniqueId == component.UniqueId)
				{
					myComponents[i] = component;
					bFound = true;
				}
			}
			if (!bFound)
				myComponents.Add(component);
		}

		/// <summary>
		/// Get component from current components cache and clean removed ones.
		/// </summary>
		/// <param name="compID">The component UniqueId</param>
		/// <returns>Found Component, null else.</returns>
		static private SmartComponent GetComponent(string compID)
		{
			SmartComponent retComponent = null;
			//Can't use foreach as collection is updated inside
			for (int i = 0; i<myComponents.Count; ++i)
			{
				SmartComponent foundComponent = myComponents[i];
				//Test if component exists as no OnUnLoad event exists.
				if ((foundComponent.ContainingProject == null)
						|| (foundComponent.ContainingProject.GetObjectFromUniqueId(foundComponent.UniqueId) == null)
						|| (foundComponent.ContainingProject.Name == ""))
				{
					Logger.AddMessage("RSLuaScript: Remove old Component " + foundComponent.Name + " from cache. This component works only with named station.", LogMessageSeverity.Information);
					myComponents.Remove(foundComponent);
					--i;
					continue;
				}

				if (foundComponent.UniqueId == compID)
				{
					retComponent = foundComponent;
				}
			}
			return retComponent;
		}

		/// <summary>
		///  Raised when a message is added.
		/// </summary>
		/// <param name="sender">Sender</param>
		/// <param name="e">The event argument.</param>
		private void OnLogMessageAdded(object sender, LogMessageAddedEventArgs e)
		{
			if (e.Message.Text.StartsWith("RSLuaScript"))
				return;

			if ( (e.Message.Text.Contains("Update RSLuaScript"))
				)
			{
				if ( DateTime.Compare(DateTime.UtcNow, lastUpdate.AddSeconds(1)) > 0 )
				{
					//Can't use foreach as collection is updated inside
					for (int i = 0; i < myComponents.Count; ++i)
					{
						SmartComponent myComponent = myComponents[i];
						//Test if component exists as no OnUnLoad event exists.
						if ((myComponent.ContainingProject == null)
								|| (myComponent.ContainingProject.GetObjectFromUniqueId(myComponent.UniqueId) == null)
								|| (myComponent.ContainingProject.Name == ""))
						{
							Logger.AddMessage("RSLuaScript: Remove old Component " + myComponent.Name + " from cache. This component works only with named station.", LogMessageSeverity.Information);
							myComponents.Remove(myComponent);
							--i;
							continue;
						}

						UpdateScriptIOSignals(myComponent);
						Logger.AddMessage("RSLuaScript: Updating Component " + myComponent.Name, LogMessageSeverity.Information);
					}
					lastUpdate = DateTime.UtcNow;
				}
			}
		}

		/// <summary>
		/// Load defined Lua Script File
		/// </summary>
		/// <param name="component">Component Owner</param>
		private void LoadFile(SmartComponent component)
		{
			string fileName = (string)component.Properties["LuaFile"].Value;

			string fileContent = string.Empty;
			bool onError = false;

			if (luaStates == null)
				luaStates = new Dictionary<string, lua_State>();

			if (!luaStates.ContainsKey(component.UniqueId))
				luaStates[component.UniqueId] = null;

			if (string.IsNullOrEmpty(fileName))
				return;

			try
			{
				using (System.IO.StreamReader reader = new System.IO.StreamReader(fileName))
				{
					fileContent = reader.ReadToEnd();
				}
			}
			catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException || ex is System.IO.IOException)
			{
				Logger.AddMessage("RSLuaScript: Can't open file: " + fileName + ".", LogMessageSeverity.Error);
				onError = true;
			}

			if (!string.IsNullOrEmpty(fileContent))
			{
				int ret = 0;
				lua_State _luaState = luaStates[component.UniqueId];
				if (!(_luaState is null))
				{
					//Already setted: clean it
					lua_close(_luaState);
				}
				_luaState = lua_open();
				luaL_openlibs(_luaState);

				//Set component parent
				lua_pushfstring(_luaState, component.UniqueId);
				lua_setglobal(_luaState, "SmartComponent");

				//Push SetStatus function
				lua_pushcfunction(_luaState, SetStatus);
				lua_setglobal(_luaState, "set_status");

				//Push AddLog function
				lua_pushcfunction(_luaState, AddLog);
				lua_setglobal(_luaState, "add_log");

				//Push ClearSignals function
				lua_pushcfunction(_luaState, ClearSignals);
				lua_setglobal(_luaState, "clear_signals");

				//Push AddSignal function
				lua_pushcfunction(_luaState, AddSignal);
				lua_setglobal(_luaState, "add_signal");

				//Push GetSignal function
				lua_pushcfunction(_luaState, GetSignal);
				lua_setglobal(_luaState, "get_signal");

				//Push SetSignal function
				lua_pushcfunction(_luaState, SetSignal);
				lua_setglobal(_luaState, "set_signal");

				//Push IIf function
				lua_pushcfunction(_luaState, IIf);
				lua_setglobal(_luaState, "iif");

				//Push GetLastSimulationTime function
				lua_pushcfunction(_luaState, GetLastSimulationTime);
				lua_setglobal(_luaState, "get_last_simulation_time");

				//Push AddIOConnection function
				lua_pushcfunction(_luaState, AddIOConnection);
				lua_setglobal(_luaState, "add_io_connection");

				//Update List for Lua script calls
				luaStates[component.UniqueId] = _luaState;
				UpdateComponentList(component);

				ret = luaL_loadbuffer(_luaState, fileContent, (uint)fileContent.Length, "program");
				if (ret != 0)
				{
					Logger.AddMessage("RSLuaScript: " + component.Name + ": Error when loading open file: " + lua_tostring(_luaState, -1)?.ToString() + ".", LogMessageSeverity.Error);
					onError = true;
				}
				else
				{
					ret = lua_pcall(_luaState, 0, 0, 0);
					if (ret != 0)
					{
						Logger.AddMessage("RSLuaScript: " + component.Name + ": Error when running main: " + lua_tostring(_luaState, -1)?.ToString() + ".", LogMessageSeverity.Error);
						onError = true;
					}
					else
						component.Properties["Status"].Value = "File loaded";
				}

			}
			if (onError)
				component.Properties["LuaFile"].Value = "";

			//Check connections on load (occurs the first time a signal change after reload component as no new connection event exists)
			if (component.ContainingProject is Station station)
				foreach (IOConnection ioConn in station.Connections)
				{
					if (!ioConn.AllowCycle)
					{
						if (ioConn.SourceObjectName == component.Name)
							Logger.AddMessage("RSLuaScript: " + component.Name + ": Connection from " + ioConn.SourceSignal + " to "
															+ ioConn.TargetObjectName + "." + ioConn.TargetSignal + " should allow cyclic connection.", LogMessageSeverity.Warning);
						if (ioConn.TargetObjectName == component.Name)
							Logger.AddMessage("RSLuaScript: " + component.Name + ": Connection from " + ioConn.SourceObjectName + "." + ioConn.SourceSignal + " to "
															+ ioConn.TargetSignal + " should allow cyclic connection.", LogMessageSeverity.Warning);
						}
				}
		}

		/// <summary>
		/// Call on_io_signal_value_changed on Lua Script
		/// </summary>
		/// <param name="component">Component owner</param>
		private void UpdateScriptIOSignals(SmartComponent component, string signalName = "", double signalNewValue = 0)
		{
			if (luaStates == null)
				luaStates = new Dictionary<string, lua_State>();

			if (!luaStates.ContainsKey(component.UniqueId))
			{
				LoadFile(component);
			}
			lua_State _luaState = luaStates[component.UniqueId];
			if (_luaState != null)
			{
				// load the function from global
				lua_getglobal(_luaState, "on_io_signal_value_changed");
				if (lua_isfunction(_luaState, -1))
				{
					lua_pushstring(_luaState, signalName);
					lua_pushnumber(_luaState, signalNewValue);
					lua_pcall(_luaState, 2, 1, 0);
					if (!lua_isnil(_luaState, -1))
						Logger.AddMessage("RSLuaScript: " + component.Name + ": on_io_signal_value_changed returns: " + lua_tostring(_luaState, -1)?.ToString() + ".", LogMessageSeverity.Error);
				}
			}
		}

		/// <summary>
		/// Call on_simulation_step on Lua Script
		/// </summary>
		/// <param name="component">Component owner</param>
		private void UpdateScriptSim(SmartComponent component)
		{
			if (luaStates == null)
				luaStates = new Dictionary<string, lua_State>();

			if (!luaStates.ContainsKey(component.UniqueId))
			{
				LoadFile(component);
			}
			lua_State _luaState = luaStates[component.UniqueId];
			if (_luaState != null)
			{
				// load the function from global
				lua_getglobal(_luaState, "on_simulation_step");
				if (lua_isfunction(_luaState, -1))
				{
					lua_pcall(_luaState, 0, 1, 0);
					if (!lua_isnil(_luaState, -1))
						Logger.AddMessage("RSLuaScript: " + component.Name + ": on_simulation_step returns: " + lua_tostring(_luaState, -1)?.ToString() + ".", LogMessageSeverity.Error);
				}
			}
		}


		//*********************************************************************************************
		/// <summary>
		/// Set text Status of component.
		/// Function called by Lua Script with 1 parameter:
		///		(string Status)
		///	Returns Nothing
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int SetStatus(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				string status = luaL_checklstring(L, 1).ToString();

				Logger.AddMessage("RSLuaScript: Lua Script set status of " + myComponent.Name + " to " + status, LogMessageSeverity.Information);

				myComponent.Properties["Status"].Value = status;
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script set status of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 0; // number of return parameters
		}

		/// <summary>
		/// Add message to RS Log.
		/// Function called by Lua Script with 1 parameter:
		///		(string Message string)
		///	Returns Nothing
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int AddLog(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				string message = luaL_checklstring(L, 1).ToString();

				Logger.AddMessage("RSLuaScript: " + myComponent.Name + ":" + message, LogMessageSeverity.Information);
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script add log from unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 0; // number of return parameters
		}

		/// <summary>
		/// Clear all signals of component.
		/// Function called by Lua Script with 0 parameter:
		///		()
		///	Returns Nothing
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int ClearSignals(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				int curs = 0;
				while (myComponent.IOSignals.Count > 1)
				{
					string signalName = myComponent.IOSignals[curs].Name;
					if (signalName != "LoadFile")
						myComponent.IOSignals.Remove(signalName);
					else
						++curs;
				}
				Logger.AddMessage("RSLuaScript: Lua Script clear all Signals of " + myComponent.Name, LogMessageSeverity.Information);

				myComponent.Properties["Status"].Value = "Cleared";
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script clear all Signals of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 0; // number of return parameters
		}

		/// <summary>
		/// Add a new signal to component.
		/// Function called by Lua Script with 2 parameters:
		///		(string Signal name, string Signal type)
		///	Returns Nothing
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int AddSignal(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				string newSignalName = luaL_checklstring(L, 1).ToString();
				string newSignalType = luaL_checklstring(L, 2).ToString();
				IOSignalType signalType = IOSignalType.DigitalInput;
				switch (newSignalType.ToLower())
				{
					case "analoginput": { signalType = IOSignalType.AnalogInput; break; }
					case "analogoutput": { signalType = IOSignalType.AnalogOutput; break; }
					case "digitalgroupinput": { signalType = IOSignalType.DigitalGroupInput; break; }
					case "digitalgroupoutput": { signalType = IOSignalType.DigitalGroupOutput; break; }
					case "digitalinput": { signalType = IOSignalType.DigitalInput; break; }
					case "digitaloutput": { signalType = IOSignalType.DigitalOutput; break; }
				}

				if (!myComponent.IOSignals.Contains(newSignalName))
				{
					IOSignal ios = new IOSignal(newSignalName, signalType)
					{
						ReadOnly = true,
					};
					myComponent.IOSignals.Add(ios);
					Logger.AddMessage("RSLuaScript: Lua Script adding Signal " + newSignalName + " to " + myComponent.Name, LogMessageSeverity.Information);
				}
				else
				{
					Logger.AddMessage("RSLuaScript: Lua Script want to add already existing Signal " + newSignalName + " to " + myComponent.Name + ". Check your lua script file.", LogMessageSeverity.Information);
				}

				myComponent.Properties["Status"].Value = "Signal Added";
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script adding Signal of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 0; // number of return parameters
		}

		/// <summary>
		/// Get a signal value from component.
		/// Function called by Lua Script with 1 parameter:
		///		(string Signal name)
		///	Returns number Signal Value
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int GetSignal(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				string signalName = luaL_checklstring(L, 1).ToString();

				if (myComponent.IOSignals.Contains(signalName))
				{
					IOSignal ios = myComponent.IOSignals[signalName];

					lua_pushnumber(L, Convert.ToDouble(ios.Value));
				}
				else
				{
					Logger.AddMessage("RSLuaScript: Lua Script get Signal value of unknown signal named:" + signalName + ". Check your script file.", LogMessageSeverity.Warning);
				}
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script get Signal value of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 1; // number of return parameters
		}

		/// <summary>
		/// Set a signal value from component.
		/// Function called by Lua Script with 2 parameters:
		///		(string Signal name, number Signal value)
		///	Returns Nothing
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int SetSignal(lua_State L)
		{
			//Parameters have to be get before
			string signalName = luaL_checklstring(L, 1).ToString();
			double signalNewValue = luaL_checknumber(L, 2);

			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				if (myComponent.IOSignals.Contains(signalName))
				{
					IOSignal ios = myComponent.IOSignals[signalName];

					switch (ios.SignalType)
					{
						case IOSignalType.DigitalOutput:
						case IOSignalType.DigitalInput:
							ios.Value = (signalNewValue != 0) ? 1 : 0;
							break;
						case IOSignalType.DigitalGroupOutput:
						case IOSignalType.DigitalGroupInput:
							ios.Value = Convert.ToInt32(signalNewValue);
							break;
						default:
							ios.Value = signalNewValue;
							break;
					}
				}
				else
				{
					Logger.AddMessage("RSLuaScript: Lua Script set Signal value of unknown signal named:" + signalName + ". Check your script file.", LogMessageSeverity.Warning);
				}
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script set Signal value of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 0; // number of return parameters
		}

		/// <summary>
		/// Ternary operator IIF
		/// Function called by Lua Script with 3 parameters:
		///		(Bool condition, Any ValueForTrue, Any ValueForFalse)
		///	Returns ValueForTrue if condition is true else ValueForFalse
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int IIf(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				if (lua_isboolean(L, 1))
				{
					lua_pushvalue(L, (lua_toboolean(L, 1) == 1) ? 2 : 3);
				}
				else
				{
					Logger.AddMessage("RSLuaScript: " + myComponent.Name + " Lua Script call iif with parameters 1 of different type than Boolean. Check your script file.", LogMessageSeverity.Warning);
				}
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script call iif of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 1; // number of return parameters
		}

		/// <summary>
		/// Get last Simulation Time from component.
		/// Function called by Lua Script with 0 parameter:
		///		()
		///	Returns double component last simulation time, double Simulator current time, double Simulator State
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int GetLastSimulationTime(lua_State L)
		{
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				if (!lastSimulTimes.ContainsKey(myComponent.UniqueId))
					lastSimulTimes[myComponent.UniqueId] = -1;

				lua_pushnumber(L, lastSimulTimes[myComponent.UniqueId]);
				lua_pushnumber(L, Simulator.CurrentTime);
				lua_pushnumber(L, (int)Simulator.State);

			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script call get_last_simulation_time of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			return 3; // number of return parameters
		}

		/// <summary>
		/// Add a new Signal IOConnection.
		/// Function called by Lua Script with 4 parameters:
		///		(string sourceObjectName, string sourceSignalName, string targetObjectName, string targetSignalName, bool allowCycle = True)
		///	Returns bool if connection created fine.
		/// </summary>
		/// <param name="L">Lua State caller</param>
		/// <returns>Number of return parameters</returns>
		static int AddIOConnection(lua_State L)
		{
			bool addDone = false;
			//Get component owner
			lua_getglobal(L, "SmartComponent");
			string compID = lua_tostring(L, -1).ToString();

			SmartComponent myComponent = GetComponent(compID);

			if (myComponent != null)
			{
				if (myComponent.ContainingProject is Station station)
				{
				  string sourceObjectName = luaL_checklstring(L, 1).ToString();
				  string sourceSignalName = luaL_checklstring(L, 2).ToString();
				  string targetObjectName = luaL_checklstring(L, 3).ToString();
				  string targetSignalName = luaL_checklstring(L, 4).ToString();
				  bool allowCycle = lua_isboolean(L, 5) ? (lua_toboolean(L, 5) != 0) : true; //Allow cyclic connection by default.

					ProjectObject sourceObject = null;
					if (sourceObjectName != "")
						sourceObject = station.FindObjects(obj => obj.Name == sourceObjectName, obj => true)?.FirstOrDefault();
					else
					{
						sourceObject = myComponent;
						sourceObjectName = myComponent.Name;
					}

					ProjectObject targetObject = null;
					if (targetObjectName != "")
						targetObject = station.FindObjects(obj => obj.Name == targetObjectName, obj => true)?.FirstOrDefault();
					else
					{
						targetObject = myComponent;
						targetObjectName = myComponent.Name;
					}

					if (sourceObject is null)
						Logger.AddMessage("RSLuaScript: " + myComponent.Name + ": Failed to find " + sourceObjectName + " as source to create new connection.", LogMessageSeverity.Warning);
					if (targetObject is null)
						Logger.AddMessage("RSLuaScript: " + myComponent.Name + ": Failed to find " + targetObjectName + " as target to create new connection.", LogMessageSeverity.Warning);

					if ((sourceObject != null) && (targetObject != null))
					{
						if ((sourceObject is IHasIOSignals) || (sourceObject is RsIrc5Controller))
						{
							if ((targetObject is IHasIOSignals) || (targetObject is RsIrc5Controller))
							{
								if (station.Connections.Where(conn => (conn.SourceObjectName == sourceObjectName) && (conn.SourceSignal == sourceSignalName)
																											&& (conn.TargetObjectName == targetObjectName) && (conn.TargetSignal == targetSignalName)
																							).Count() > 0)
								{
									Logger.AddMessage("RSLuaScript: " + myComponent.Name + ": Failed to create connection from " + sourceObjectName + "." + sourceSignalName 
																																					+ " to " + targetObjectName + "." + targetSignalName + ", because it already exists.", LogMessageSeverity.Warning);
								}
								else
								{
									IOConnection ioConn = new IOConnection(sourceObject, sourceSignalName, targetObject, targetSignalName, allowCycle);
									station.Connections.Add(ioConn);
									addDone = true;
								}
							}
							else
								Logger.AddMessage("RSLuaScript: " + myComponent.Name + ": " + targetObjectName + " should be either a IHasIOSignals or an RsIrc5Controller.", LogMessageSeverity.Warning);
						}
						else
							Logger.AddMessage("RSLuaScript: " + myComponent.Name + ": " + sourceObjectName + " should be either a IHasIOSignals or an RsIrc5Controller.", LogMessageSeverity.Warning);
					}
				}
			}
			else
			{
				Logger.AddMessage("RSLuaScript: Lua Script call get_last_simulation_time of unknown component. Closing it.", LogMessageSeverity.Error);
				lua_close(L);
			}

			lua_pushboolean(L, addDone ? 1 : 0);
			return 1; // number of return parameters
		}


	}//public class CodeBehind : SmartComponentCodeBehind
}

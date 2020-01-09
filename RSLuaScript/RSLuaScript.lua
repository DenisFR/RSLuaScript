-- RSLuaScript Example File
-- This component use KopiLua library you can get here:
-- https://github.com/Myndale/KopiLua
-- Very important: Lua scripts must be encoded as ASCII files!
-- 
-- You can get Lua manual here:
-- https://www.lua.org/manual/5.1/manual.html
--
--Pushed functions:
--  set_status(string status): Set the status of component
--  add_log(string message): Add new message to RS Log
--  clear_signals(): Remove all signals of component
--  add_signal(string SignalName, string SignalType ["AnalogInput","AnalogOutput","DigitalGroupInput","DigitalGroupOutput","DigitalInput","DigitalOutput"]): Add a new signal to component
--  get_signal(string SignalName): Get value of a signal. Returns double value.
--  set_signal(string SignalName, double SignalValue): Set value of a signal.
--  iif(bool condition, any ValueForTrue, any ValueForFalse): Returns ValueForTrue if condition is true else ValueForFalse.
--  get_last_simulation_time(): Returns double component last simulation time ms, double Simulator current time ms, double Simulator State (0:Init, 1:Paused, 2:Ready, 3:Running, 4:Shutdown, 5:Stopped)
--  add_io_connection(string sourceObjectName, string sourceSignalName, string targetObjectName, string targetSignalName, bool allowCycle = True): Create a new connection from source to target. If object name is "" then the owner SmartComponent is used. Returns if ceration is successful.
--
--Analog Signal: double value -1.7976931348623157E+308 to 1.7976931348623157E+308 epsilon 4.94065645841247E-324
--DigitalGroup Signal: integer value -2147483648 to 2147483647
--Digital Signal: 0 to 1

-----------------------------------------
-- Here your own variables declaration --
-----------------------------------------

local lastCompSimTime = 0
local lastSimTime = 0
local simulState = 0

----------------------------------------
-- Here functions called by component --
-- You can edit but DON'T REMOVE THEM --
----------------------------------------

-- Called each time a signal is changed
-- Returned value will be added to Log
function on_io_signal_value_changed(signal_name, new_signal_value)
  add_log("on_io_signal_value_changed called with"
         .. " signal_name = " .. signal_name
         .. ", new_signal_value = " .. new_signal_value .. ".")
  lastCompSimTime, lastSimTime, simulState = get_last_simulation_time()
  add_log("on io Simul Time:" .. lastCompSimTime .. " - " .. lastSimTime .. " - " .. simulState)
  
  local signali1 = get_signal("SignalI1")
  local signali2 = get_signal("SignalI2")
  local res
  if (signali1 == signali2) then res = 1 else res = 0 end
  set_signal("SignalO1", res)
  set_signal("SignalO2", iif(signali1 == signali2, 0, 1))
  
  ComputeGroupValues()
  
  set_status("Signals Updated")
  return "Exit on_io_signal_value_changed"
end

-- Called each simulation step
-- Returned value will be added to Log
function on_simulation_step()
  set_status("on_simulation_step called.")
  ComputeAnalogValues()
  ComputeGroupValues()
end

----------------------------------------
-- Here your own function declaration --
----------------------------------------

function ComputeAnalogValues()
  local signalai1 = get_signal("SignalAI1")
  local signalai2 = get_signal("SignalAI2")
  
  set_signal("SignalAO1",signalai1 + signalai2)
  set_signal("SignalAO2",signalai1 - signalai2)
end

function ComputeGroupValues()
  local signalgi1 = get_signal("SignalGI1")
  local signalgi2 = get_signal("SignalGI2")
  
  set_signal("SignalGO1",signalgi1 + signalgi2)
  set_signal("SignalGO2",signalgi1 - signalgi2)

end

-----------------------------------
-- Here your code called at load --
-----------------------------------
clear_signals()
add_signal("SignalI1","DigitalInput")
add_signal("SignalI2","DigitalInput")
add_signal("SignalO1","DigitalOutput")
add_signal("SignalO2","DigitalOutput")
add_signal("SignalAI1","AnalogInput")
add_signal("SignalAI2","AnalogInput")
add_signal("SignalAO1","AnalogOutput")
add_signal("SignalAO2","AnalogOutput")
add_signal("SignalGI1","DigitalGroupInput")
add_signal("SignalGI2","DigitalGroupInput")
add_signal("SignalGO1","DigitalGroupOutput")
add_signal("SignalGO2","DigitalGroupOutput")
add_io_connection("","SignalO1","","SignalI1")
add_io_connection("RSLuaScript","SignalO1","RSLuaScript","SignalI1")
add_io_connection("STERE","SignalO1","jkdhsid","SignalI1")

add_log("Example Script File Loaded")

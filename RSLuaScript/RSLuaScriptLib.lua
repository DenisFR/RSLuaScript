---@meta
-- RSLuaScript Library File
-- This component use KopiLua library you can get here:
-- https://github.com/Myndale/KopiLua
-- Very important: Lua scripts must be encoded as ASCII files!
-- 
-- You can get Lua manual here:
-- https://www.lua.org/manual/5.1/manual.html
--
--Pushed functions:

---Set the status of component
---@param status string
function set_status(status)end

---Add new message to RS Log
---@param message string
function add_log(message)end

---Remove all signals of component
function clear_signals()end

---Add a new signal to component
---@param SignalName string
---@param SignalType string
---|"AnalogInput"
---|"AnalogOutput"
---|"DigitalGroupInput"
---|"DigitalGroupOutput"
---|"DigitalInput"
---|"DigitalOutput"
function add_signal(SignalName,SignalType)end

---Get value of a signal
---@param SignalName any
---@return number SignalValue
function get_signal(SignalName) return 2.0 end

---Set value of a signal
---@param SignalName string
---@param SignalValue number
---@param ReadOnly? boolean To mark signal as Read Only (True by default)
function set_signal(SignalName,SignalValue,ReadOnly)end

---Set object visibility if Visibility is not 0.
--- Use its "{guid}", "*" to get selected guid or composed name ("parent/objectname")
---@param ObjectName string
---@param Visibility integer
---@return boolean Visibility
function set_visibility(ObjectName,Visibility) return true end

---Returns ValueForTrue if condition is true else ValueForFalse.
---@param condition boolean
---@param ValueForTrue any
---@param ValueForFalse any
---@return any
function iif(condition,ValueForTrue,ValueForFalse) return ValueForTrue end

---Returns double component , double , double 
---@return number last component last simulation time ms
---@return number current Simulator current time ms
---@return number state Simulator State (0:Init, 1:Paused, 2:Ready, 3:Running, 4:Shutdown, 5:Stopped)
function get_last_simulation_time() return 0.0,0.0,0.0 end

---Create a new connection from source to target.
---@param sourceObjectName string If object name is "" then the owner SmartComponent is used.
---@param sourceSignalName string Signal name to connect from.
---@param targetObjectName string If object name is "" then the owner SmartComponent is used.
---@param targetSignalName string Signal name to connect to.
---@param allowCycle? boolean To allow Cyclic Connection (True by default)
---@return boolean status If creation is successful
function add_io_connection(sourceObjectName,sourceSignalName,targetObjectName,targetSignalName,allowCycle) return true end

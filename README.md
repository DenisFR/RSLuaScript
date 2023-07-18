# RSLuaScript
This is a RobotStudio Smart Component to run Lua script inside RS.
With it, you can add signals to manage your simulation.

## What you have to do before compiling:
  - Install NuGet package [**KopiLua.1.0.6752.15716**](//https://www.nuget.org/packages/KopiLua/1.0.6752.15716)
  - Update ABB.Robotics.* References to Good RobotStudio SDK Version path with ***Project*** - ***Add Reference*** - ***Browse***.
  - On Project Properties:
    - **Application**: Choose good .NET Framework version.
    - **Build Events**: *Post Build Events*: Replace with the good LibraryCompiler.exe Path.
    - **Debug**: *Start External Program*: Replace with the good RobotStudio.exe Path `This not work if project on network drive, let it clear.`
  - In *\RSLuaScript\RSLuaScript.en.xml*:
    - Replace **xsi:schemaLocation** value with good one.
  - Same for *\RSLuaScript\RSLuaScript.xml*.

### If your project path is on network drive:
##### To get RobotStudio load it:
  - In *$(RobotStudioPath)\Bin\RobotStudio.exe.config* file:
    - Add in section *`<configuration><runtime>`*
      - `<loadFromRemoteSources enable="true"/>`

##### To Debug it:
  - Start first RobotStudio to get RobotStudio.exe.config loaded.
  - Then attach its process in VisualStudio ***Debug*** - ***Attach to Process..***

## Usage
![RSLuaScript](https://raw.githubusercontent.com/DenisFR/RSLuaScript/master/RSLuaScript/RSLuaScript.jpg)
### Signals
  - ***LoadFile***:\
Set to high (1) to load your Lua script file.  
If you reload the same file, a message ask you if you want to re-execute main program.

The first time RSLuaScript runs, it install KopiLua.dll to your %USERPROFILE%\Documents\RobotStudio\Libraries directory.

You have to run it on named station. Else, a log appears with message:  
>*"RSLuaScript: Remove old Component MyRSLuaScriptName from cache. This component works only with named station."*

RSLuaScript copy the exemple file (*RSLuaScript.lua*) to your station directory.

***Very important: Lua scripts must be encoded as ASCII files!***

Once your script linked, you can connect added signal to your station in *Station Logic* like other components.  
It's recommended to Allow cyclic connection. Your can change this property in *Signal and Connections* - *I/O Connections* frame.

You can get Lua manual here:  
(https://www.lua.org/manual/5.1/manual.html)


## Pushed functions:

  - *set_status(string status)*: Set the status of component
  - *add_log(string message)*: Add new message to RS Log
  - *clear_signals()*: Remove all signals of component
  - *add_signal(string SignalName, string SignalType ["AnalogInput","AnalogOutput","DigitalGroupInput","DigitalGroupOutput","DigitalInput","DigitalOutput"], bool ReadOnly = True)*: Add a new signal to component
  - *get_signal(string SignalName)*: Get value of a signal. Returns double value.
  - *set_signal(string SignalName, double SignalValue)*: Set value of a signal.
  - *set_visibility(string ObjectName, double Visibility)*: Set object visibility if Visibility is not 0. Use its "\{guid\}", "*" to get selected guid or composed name ("parent/objectname")
  - *iif(Bool condition, Any ValueForTrue, Any ValueForFalse)*: Returns ValueForTrue if condition is true else ValueForFalse.
  - *get_last_simulation_time()*: Returns double component last simulation time ms, double Simulator current time ms, double Simulator State (0:Init, 1:Paused, 2:Ready, 3:Running, 4:Shutdown, 5:Stopped)
  - *add_io_connection(string sourceObjectName, string sourceSignalName, string targetObjectName, string targetSignalName, bool allowCycle = True)*: Create a new connection from source to target. If object name is "" then the owner SmartComponent is used. Returns if creation is successful.


**Analog Signal**: double value -1.7976931348623157E+308 to 1.7976931348623157E+308 epsilon 4.94065645841247E-324  
**DigitalGroup Signal**: integer value -2147483648 to 2147483647  
**Digital Signal**: 0 to 1  

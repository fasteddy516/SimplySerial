# SimplySerial

###### A serial terminal that runs as a Windows console application.
  
Written by [Edward Wright](mailto:fasteddy@thewrightspace.net) (fasteddy516).

Available at https://github.com/fasteddy516/SimplySerial


# Description

SimplySerial is a basic serial terminal that runs as a Windows console application.  It provides a quick way to connect to - and communicate with - serial devices through the Windows Command Prompt or PowerShell.  SimplySerial can be used directly from Command Prompt/PowerShell and should work with most devices that appear in Device Manager as "COMx".  It was, however, written specifically for
use within a "terminal" window in [Visual Studio Code](https://code.visualstudio.com/) to provide serial communications with devices running [CircuitPython](https://circuitpython.org/).  Most of the testing and development of this application was done with this use case in mind.  


# Requirements

* Windows 7, 8, 8.1 or 10
* .NET Framework 4.5 or newer

_The required version of .NET framework is already included in Windows 8 and newer.  If you're running Windows 7, you may need to download and install it from Microsoft at https://dotnet.microsoft.com/download/dotnet-framework._


# Installation

Download the [latest release](https://github.com/fasteddy516/SimplySerial/releases/latest) of this application in one of three formats:

`SimplySerial_x.x.x_user_setup.msi` is a windows installer package that puts everything where it needs to go and adds the location of the SimplySerial executable to your `PATH` environment variable, which makes it easily accessible from Command Prompt, PowerShell and Visual Studio Code.  Installation is per-user, and does not require Administrative rights to install.  **This is the preferred installation method,** _and works well with the "user setup" version of VSCode_.

`SimplySerial_x.x.x_system_setup.msi` is similar to `user_setup.msi` except that the installation is system-wide (for all users), and **requires administrative rights to install.**  _This version will work with both the "user setup" and "system setup" versions of VSCode_.

**_If you are unsure which version of VSCode you have installed, load it up and go to `Help > About` - beside the version number it will say either `user` or `system` setup._**

**_The installer versions are unsigned, and may trigger a "Windows Defender SmartScreen" warning. To install you have to press "More Info" followed by "Run Anyway"._**

`SimplySerial_x.x.x_standalone.zip` is a standard compressed archive containing the SimplySerial executable and some documentation.  You can unzip it whereever you like, and add that location to your `PATH` or not.  **Advanced users may prefer this format/process.**


# Using SimplySerial

For CircuitPython users, type `ss` in a Command Prompt, PowerShell or VSCode Terminal Window and press `enter`.  That's it!

By default, SimplySerial will attempt to identify and connect to a CircuitPython-capable board at 9600 baud, no parity, 8 data bits and 1 stop bit.  If no known boards are detected, it will default to the first available serial (COM) port.  If there are no COM ports available, it will wait until one shows up, then connect to it. 

Once you're connected, you should see messages from the device on COMx appear on screen, and anything you type into Command Prompt/PowerShell will be sent to the device.  CircuitPython users can access the REPL using `CTRL-C` and exit the REPL using `CTRL-D`.

You can exit SimplySerial any time by pressing `CTRL-X`.  

If you have multiple COM ports, multiple CircuitPython devices connected, or need to use different communications settings, you will need to use the appropriate command-line arguments listed below:

  `-h, --help` displays a list of valid command-line arguments

  `-l, --list` displays a list of available COM ports  

  `-c, --com` sets the desired COM port (ex. `-c:1` for COM1, `--com:22` for COM22)

  `-b, --baud` sets the baud rate (ex. `-b:9600`, `--baud:115200`)

  `-p, --parity` sets the parity option (ex. `-p:none`, `--parity:even`) 
  
  `-d, --databits` sets the number of data bits to use (ex. `-d:8`, `--databits:7`)

  `-s, --stopbits` sets the number of stop bits to use (ex. `-s:1`, `--stopbits:1.5`)

  `-a, --autoconnect` sets the desired auto-(re)connect behaviour (ex. `a:NONE`, `--autoconnect:ANY`)
  
  `-q, --quiet` prevents any application messages (connection banner, error messages, etc.) from printing out to the console.

If you wanted to connect to a device on COM17 at 115200 baud, you would use the command `ss -c:17 -b:115200`, or if you really enjoy typing `ss --com:17 --baud:115200`.


# Auto-(re)connect functionality

  SimplySerial's `autoconnect` option can be used to determine if and how to connect/reconnect to a device.  These options function as follows:
  
  `--autoconnect:ONE` is the default mode of operation.  If a COM port was specified using the `--com` option, SimplySerial will attempt to connect to the specified port, otherwise it will connect to the first available COM port (giving preference to devices known to be CircuitPython-capable).  In either case, the program will wait until the/a COM port is available, and connect to it when it is.  If the device becomes unavailable at any point (because it was disconnected, etc.), SimplySerial will wait until that specific COM port becomes available again, regardless of any other COM ports that may or may not be available.
  
  `--autoconnect:ANY` is similar to `ONE`, except that when the connected port becomes unavailable, SimplySerial will attempt to connect to any other available port.  This option is useful if you only ever have one COM port available at a time, but can be problematic if you have multiple COM ports connected, or if you have a built-in COM port that is always available.
  
  `--autoconnect:NONE` prevents SimplySerial from waiting for devices and automatically re-connecting.
  

# Using SimplySerial in Visual Studio Code (VSCode)

  In a standard installation of VSCode, opening a "terminal" gets you a Command Prompt or PowerShell window embedded in the VSCode interface.  SimplySerial works exactly the same within this embedded window as it does in a normal Command Prompt or PowerShell, which means using SimplySerial within VSCode is as easy as opening a terminal window via the menu bar (`Terminal > New Terminal`) or shortcut key, typing `ss` and pressing enter.

  If you want to make things even simpler, or if you need to use a bunch of command-line arguments and don't want to enter them every time (**and you don't use the terminal window in Visual Studio Code for anything else**) you can have VSCode launch SimplySerial directly whenever you open a terminal window by changing the `terminal.integrated.shell.windows` setting to point to `ss.exe` + any arguments you need to add.  This works well, but will prevent you from having multiple VSCode terminal windows open, as only one application can connect to any given serial port at a given time.


# Contributing

  If you have questions, problems, feature requests, etc. please post them to the [Issues section on Github](https://github.com/fasteddy516/SimplySerial/issues).  If you would like to contribute, please let me know.  I have already put some "enhancement requests" in the Github Issues section with some ideas for improvements, most of which were either beyond my limited C#/Windows programming knowledge, or required more time than I had available! 


# Acknowledgements

  The code used to obtain extra details about connected serial devices (VID, PID, etc.) is a modified version of examples posted by Kamil Górski (freakone) at http://blog.gorski.pm/serial-port-details-in-c-sharp and https://github.com/freakone/serial-reader.  Some modifications were made based on this stackoverflow thread: https://stackoverflow.com/questions/11458835/finding-information-about-all-serial-devices-connected-through-usb-in-c-sharp.
